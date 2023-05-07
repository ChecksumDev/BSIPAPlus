#nullable enable
using IPA.Config.Data;
using IPA.Config.Stores;
using IPA.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
#if NET3
using Net3_Proxy;
using Array = Net3_Proxy.Array;
#endif

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace IPA.Config.Stores
{
    internal static partial class GeneratedStoreImpl
    {
        internal interface IGeneratedStore
        {
            Type Type { get; }
            IGeneratedStore Parent { get; }
            Impl Impl { get; }
            void OnReload();

            void Changed();
            IDisposable ChangeTransaction();

            Value Serialize();
            void Deserialize(Value val);
        }

        internal interface IGeneratedStore<T> : IGeneratedStore where T : class
        {
            void CopyFrom(T source, bool useLock);
        }

        internal interface IGeneratedPropertyChanged : INotifyPropertyChanged
        {
            PropertyChangedEventHandler PropertyChangedEvent { get; }
        }

        internal class Impl : IConfigStore
        {
            internal static ConstructorInfo Ctor = typeof(Impl).GetConstructor(new[] { typeof(IGeneratedStore) });
            internal static MethodInfo ImplGetSyncObjectMethod = typeof(Impl).GetMethod(nameof(ImplGetSyncObject));

            internal static MethodInfo ImplGetWriteSyncObjectMethod =
                typeof(Impl).GetMethod(nameof(ImplGetWriteSyncObject));

            internal static MethodInfo ImplSignalChangedMethod = typeof(Impl).GetMethod(nameof(ImplSignalChanged));

            internal static MethodInfo ImplInvokeChangedMethod = typeof(Impl).GetMethod(nameof(ImplInvokeChanged));

            internal static MethodInfo ImplTakeReadMethod = typeof(Impl).GetMethod(nameof(ImplTakeRead));

            internal static MethodInfo ImplReleaseReadMethod = typeof(Impl).GetMethod(nameof(ImplReleaseRead));

            internal static MethodInfo ImplTakeWriteMethod = typeof(Impl).GetMethod(nameof(ImplTakeWrite));

            internal static MethodInfo ImplReleaseWriteMethod = typeof(Impl).GetMethod(nameof(ImplReleaseWrite));

            internal static MethodInfo ImplChangeTransactionMethod =
                typeof(Impl).GetMethod(nameof(ImplChangeTransaction));

            // TODO: maybe sometimes clean this?
            private static readonly Stack<ChangeTransactionObj> freeTransactionObjs = new();

            internal static MethodInfo ImplReadFromMethod = typeof(Impl).GetMethod(nameof(ImplReadFrom));

            internal static MethodInfo ImplWriteToMethod = typeof(Impl).GetMethod(nameof(ImplWriteTo));
            private readonly IGeneratedStore generated;

            private readonly AutoResetEvent resetEvent = new(false);
            private long enteredTransactions;

            public Impl(IGeneratedStore store)
            {
                generated = store;
            }

            public WaitHandle SyncObject => resetEvent;

            public ReaderWriterLockSlim WriteSyncObject { get; } = new();

            public void ReadFrom(ConfigProvider provider)
            {
                Logger.Config.Debug($"Generated impl ReadFrom {generated.GetType()}");
                Value? values = provider.Load();
                //Logger.config.Debug($"Read {values}");
                generated.Deserialize(values);

                using IDisposable? transaction = generated.ChangeTransaction();
                generated.OnReload();
            }

            public void WriteTo(ConfigProvider provider)
            {
                Logger.Config.Debug($"Generated impl WriteTo {generated.GetType()}");
                Value? values = generated.Serialize();
                //Logger.config.Debug($"Serialized {values}");
                provider.Store(values);
            }

            public static WaitHandle? ImplGetSyncObject(IGeneratedStore s)
            {
                return FindImpl(s)?.SyncObject;
            }

            public static ReaderWriterLockSlim? ImplGetWriteSyncObject(IGeneratedStore s)
            {
                return FindImpl(s)?.WriteSyncObject;
            }

            public static void ImplSignalChanged(IGeneratedStore s)
            {
                FindImpl(s)?.SignalChanged();
            }

            public void SignalChanged()
            {
                try
                {
                    _ = resetEvent.Set();
                }
                catch (ObjectDisposedException e)
                {
                    Logger.Config.Error(
                        $"ObjectDisposedException while signalling a change for generated store {generated?.GetType()}");
                    Logger.Config.Error(e);
                }
            }

            public static void ImplInvokeChanged(IGeneratedStore s)
            {
                FindImpl(s)?.InvokeChanged();
            }

            public void InvokeChanged()
            {
                generated.Changed();
            }

            public static void ImplTakeRead(IGeneratedStore s)
            {
                FindImpl(s)?.TakeRead();
            }

            public void TakeRead()
            {
                if (!WriteSyncObject.IsWriteLockHeld)
                {
                    WriteSyncObject.EnterReadLock();
                }
            }

            public static void ImplReleaseRead(IGeneratedStore s)
            {
                FindImpl(s)?.ReleaseRead();
            }

            public void ReleaseRead()
            {
                if (!WriteSyncObject.IsWriteLockHeld)
                {
                    WriteSyncObject.ExitReadLock();
                }
            }

            public static void ImplTakeWrite(IGeneratedStore s)
            {
                FindImpl(s)?.TakeWrite();
            }

            public void TakeWrite()
            {
                WriteSyncObject.EnterWriteLock();
            }

            public static void ImplReleaseWrite(IGeneratedStore s)
            {
                FindImpl(s)?.ReleaseWrite();
            }

            public void ReleaseWrite()
            {
                WriteSyncObject.ExitWriteLock();
            }

            public static IDisposable? ImplChangeTransaction(IGeneratedStore s, IDisposable nest)
            {
                return FindImpl(s)?.ChangeTransaction(nest);
            }

            // TODO: improve trasactionals so they don't always save in every case
            public IDisposable ChangeTransaction(IDisposable nest, bool takeWrite = true)
            {
                return GetFreeTransaction().InitWith(this, nest, takeWrite && !WriteSyncObject.IsWriteLockHeld);
            }

            private static ChangeTransactionObj GetFreeTransaction()
            {
                return freeTransactionObjs.Count > 0
                    ? freeTransactionObjs.Pop()
                    : new ChangeTransactionObj();
            }

            public static Impl? FindImpl(IGeneratedStore store)
            {
                while (store?.Parent != null)
                {
                    store = store.Parent; // walk to the top of the tree
                }

                return store?.Impl;
            }

            public static void ImplReadFrom(IGeneratedStore s, ConfigProvider provider)
            {
                FindImpl(s)?.ReadFrom(provider);
            }

            public static void ImplWriteTo(IGeneratedStore s, ConfigProvider provider)
            {
                FindImpl(s)?.WriteTo(provider);
            }

            private sealed class ChangeTransactionObj : IDisposable
            {
                private Data data;

                public void Dispose()
                {
                    Dispose(true);
                }

                public ChangeTransactionObj InitWith(Impl impl, IDisposable nest, bool takeWrite)
                {
                    data = new Data(impl, takeWrite, nest);

                    _ = Interlocked.Increment(ref impl.enteredTransactions);
                    if (data.ownsWrite)
                    {
                        impl.TakeWrite();
                    }

                    return this;
                }

                private void Dispose(bool addToStore)
                {
                    if (data.impl != null && Interlocked.Decrement(ref data.impl.enteredTransactions) == 0)
                    {
                        data.impl.InvokeChanged();
                    }

                    data.nested?.Dispose();
                    try
                    {
                        if (data.ownsWrite)
                        {
                            data.impl?.ReleaseWrite();
                        }
                    }
                    catch
                    {
                    }

                    data = default;

                    if (addToStore)
                    {
                        freeTransactionObjs.Push(this);
                    }
                }

                ~ChangeTransactionObj()
                {
                    Dispose(false);
                }

                private struct Data
                {
                    public readonly Impl impl;
                    public readonly bool ownsWrite;
                    public readonly IDisposable nested;

                    public Data(Impl impl, bool takeWrite, IDisposable nest)
                    {
                        this.impl = impl;
                        ownsWrite = takeWrite;
                        nested = nest;
                    }
                }
            }
        }
    }
}