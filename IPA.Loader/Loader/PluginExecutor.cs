using IPA.Logging;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
#if NET4
using Task = System.Threading.Tasks.Task;
using TaskEx = System.Threading.Tasks.Task;
using Expression = System.Linq.Expressions.Expression;
using ExpressionEx = System.Linq.Expressions.Expression;
#endif

#if NET3
using System.Threading.Tasks;
using Net3_Proxy;
using Path = Net3_Proxy.Path;
using File = Net3_Proxy.File;
using Directory = Net3_Proxy.Directory;
#endif

namespace IPA.Loader
{
    // NOTE: TaskEx.WhenAll() (Task.WhenAll() in .NET 4) returns CompletedTask if it has no arguments, which we need for .NET 3
    internal class PluginExecutor
    {
        public enum Special
        {
            None, Self, Bare
        }

        public PluginExecutor(PluginMetadata meta, Special specialType = Special.None)
        {
            Metadata = meta;
            SpecialType = specialType;
            if (specialType != Special.None)
            {
                CreatePlugin = m => null;
                LifecycleEnable = o => { };
                LifecycleDisable = o => TaskEx.WhenAll();
            }
            else
            {
                PrepareDelegates();
            }
        }

        public PluginMetadata Metadata { get; }
        public Special SpecialType { get; }


        public object Instance { get; private set; }
        private Func<PluginMetadata, object> CreatePlugin { get; set; }

        private Action<object> LifecycleEnable { get; set; }

        // disable may be async (#24)
        private Func<object, Task> LifecycleDisable { get; set; }

        public void Create()
        {
            if (Instance != null)
            {
                return;
            }

            Instance = CreatePlugin(Metadata);
        }

        public void Enable()
        {
            LifecycleEnable(Instance);
        }

        public Task Disable()
        {
            return LifecycleDisable(Instance);
        }


        private void PrepareDelegates()
        {
            // TODO: use custom exception types or something
            PluginLoader.Load(Metadata);
            Type type = Metadata.Assembly.GetType(Metadata.PluginType.FullName);

            CreatePlugin = MakeCreateFunc(type, Metadata.Name);
            LifecycleEnable = MakeLifecycleEnableFunc(type, Metadata.Name);
            LifecycleDisable = MakeLifecycleDisableFunc(type, Metadata.Name);
        }

        private static Func<PluginMetadata, object> MakeCreateFunc(Type type, string name)
        {
            // TODO: what do i want the visibiliy of Init methods to be?
            ConstructorInfo[] ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Select(c => (c, attr: c.GetCustomAttribute<InitAttribute>()))
                .NonNull(t => t.attr)
                .OrderByDescending(t => t.c.GetParameters().Length)
                .Select(t => t.c).ToArray();
            if (ctors.Length > 1)
            {
                Logger.Loader.Warn(
                    $"Plugin {name} has multiple [Init] constructors. Picking the one with the most parameters.");
            }

            bool usingDefaultCtor = false;
            ConstructorInfo ctor = ctors.FirstOrDefault();
            if (ctor == null)
            {
                // this is a normal case
                usingDefaultCtor = true;
                ctor = type.GetConstructor(Type.EmptyTypes);
                if (ctor == null)
                {
                    throw new InvalidOperationException(
                        $"{type.FullName} does not expose a public default constructor and has no constructors marked [Init]");
                }
            }

            MethodInfo[] initMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => (m, attr: m.GetCustomAttribute<InitAttribute>()))
                .NonNull(t => t.attr).Select(t => t.m).ToArray();
            // verify that they don't have lifecycle attributes on them
            foreach (MethodInfo method in initMethods)
            {
                object[] attrs = method.GetCustomAttributes(typeof(IEdgeLifecycleAttribute), false);
                if (attrs.Length != 0)
                {
                    throw new InvalidOperationException(
                        $"Method {method} on {type.FullName} has both an [Init] attribute and a lifecycle attribute.");
                }
            }

            ParameterExpression metaParam = Expression.Parameter(typeof(PluginMetadata), "meta");
            ParameterExpression objVar = ExpressionEx.Variable(type, "objVar");
            ParameterExpression persistVar = ExpressionEx.Variable(typeof(object), "persistVar");
            Expression<Func<PluginMetadata, object>> createExpr = Expression.Lambda<Func<PluginMetadata, object>>(
                ExpressionEx.Block(new[] { objVar, persistVar },
                    initMethods
                        .Select(m => PluginInitInjector.InjectedCallExpr(m.GetParameters(), metaParam, persistVar,
                            es => Expression.Call(objVar, m, es)))
                        .Prepend(ExpressionEx.Assign(objVar,
                            usingDefaultCtor
                                ? Expression.New(ctor)
                                : PluginInitInjector.InjectedCallExpr(ctor.GetParameters(), metaParam, persistVar,
                                    es => Expression.New(ctor, es))))
                        .Append(Expression.Convert(objVar, typeof(object)))),
                metaParam);
            // TODO: since this new system will be doing a fuck load of compilation, maybe add FastExpressionCompiler
            return createExpr.Compile();
        }

        // TODO: make enable and disable able to take a bool indicating which it is
        private static Action<object> MakeLifecycleEnableFunc(Type type, string name)
        {
            bool noEnableDisable = type.GetCustomAttribute<NoEnableDisableAttribute>() is not null;
            MethodInfo[] enableMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => (m, attrs: m.GetCustomAttributes(typeof(IEdgeLifecycleAttribute), false)))
                .Select(t => (t.m, attrs: t.attrs.Cast<IEdgeLifecycleAttribute>()))
                .Where(t => t.attrs.Any(a => a.Type == EdgeLifecycleType.Enable))
                .Select(t => t.m).ToArray();
            if (enableMethods.Length == 0)
            {
                if (!noEnableDisable)
                {
                    Logger.Loader.Notice(
                        $"Plugin {name} has no methods marked [OnStart] or [OnEnable]. Is this intentional?");
                }

                return o => { };
            }

            foreach (MethodInfo m in enableMethods)
            {
                if (m.GetParameters().Length > 0)
                {
                    throw new InvalidOperationException(
                        $"Method {m} on {type.FullName} is marked [OnStart] or [OnEnable] and has parameters.");
                }

                if (m.ReturnType != typeof(void))
                {
                    Logger.Loader.Warn(
                        $"Method {m} on {type.FullName} is marked [OnStart] or [OnEnable] and returns a value. It will be ignored.");
                }
            }

            ParameterExpression objParam = Expression.Parameter(typeof(object), "obj");
            ParameterExpression instVar = ExpressionEx.Variable(type, "inst");
            Expression<Action<object>> createExpr = Expression.Lambda<Action<object>>(
                ExpressionEx.Block(new[] { instVar },
                    enableMethods
                        .Select(m => (Expression)Expression.Call(instVar, m))
                        .Prepend(ExpressionEx.Assign(instVar, Expression.Convert(objParam, type)))),
                objParam);
            return createExpr.Compile();
        }

        private static Func<object, Task> MakeLifecycleDisableFunc(Type type, string name)
        {
            bool noEnableDisable = type.GetCustomAttribute<NoEnableDisableAttribute>() is not null;
            MethodInfo[] disableMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => (m, attrs: m.GetCustomAttributes(typeof(IEdgeLifecycleAttribute), false)))
                .Select(t => (t.m, attrs: t.attrs.Cast<IEdgeLifecycleAttribute>()))
                .Where(t => t.attrs.Any(a => a.Type == EdgeLifecycleType.Disable))
                .Select(t => t.m).ToArray();
            if (disableMethods.Length == 0)
            {
                if (!noEnableDisable)
                {
                    Logger.Loader.Notice(
                        $"Plugin {name} has no methods marked [OnExit] or [OnDisable]. Is this intentional?");
                }

                return o => TaskEx.WhenAll();
            }

            List<MethodInfo> taskMethods = new();
            List<MethodInfo> nonTaskMethods = new();
            foreach (MethodInfo m in disableMethods)
            {
                if (m.GetParameters().Length > 0)
                {
                    throw new InvalidOperationException(
                        $"Method {m} on {type.FullName} is marked [OnExit] or [OnDisable] and has parameters.");
                }

                if (m.ReturnType != typeof(void))
                {
                    if (typeof(Task).IsAssignableFrom(m.ReturnType))
                    {
                        taskMethods.Add(m);
                        continue;
                    }

                    Logger.Loader.Warn(
                        $"Method {m} on {type.FullName} is marked [OnExit] or [OnDisable] and returns a non-Task value. It will be ignored.");
                }

                nonTaskMethods.Add(m);
            }

            Expression<Func<Task>> completedTaskDel = () => TaskEx.WhenAll();
            Expression getCompletedTask = completedTaskDel.Body;
            MethodInfo taskWhenAll = typeof(TaskEx).GetMethod(nameof(TaskEx.WhenAll), new[] { typeof(Task[]) });

            ParameterExpression objParam = Expression.Parameter(typeof(object), "obj");
            ParameterExpression instVar = ExpressionEx.Variable(type, "inst");
            Expression<Func<object, Task>> createExpr = Expression.Lambda<Func<object, Task>>(
                ExpressionEx.Block(new[] { instVar },
                    nonTaskMethods
                        .Select(m => (Expression)Expression.Call(instVar, m))
                        .Prepend(ExpressionEx.Assign(instVar, Expression.Convert(objParam, type)))
                        .Append(
                            taskMethods.Count == 0
                                ? getCompletedTask
                                : Expression.Call(taskWhenAll,
                                    Expression.NewArrayInit(typeof(Task),
                                        taskMethods.Select(m =>
                                            (Expression)Expression.Convert(Expression.Call(instVar, m),
                                                typeof(Task))))))),
                objParam);
            return createExpr.Compile();
        }
    }
}