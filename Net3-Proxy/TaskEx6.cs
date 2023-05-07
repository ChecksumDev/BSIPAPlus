using System;
using System.Threading.Tasks;

namespace Net3_Proxy
{
    public static class TaskEx6
    {
        public static Task<TResult> FromException<TResult>(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
            tcs.TrySetException(exception);
            return tcs.Task;
        }

        public static Task FromException(Exception exception)
        {
            return FromException<VoidTaskResult>(exception);
        }

        private struct VoidTaskResult
        {
        }
    }
}