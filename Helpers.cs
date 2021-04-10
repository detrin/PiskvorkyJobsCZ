using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PiskvorkyJobsCZ
{
    public static class Extensions
    {
        /// <summary>
        /// A helper method to make Process.WaitForExit async.
        /// </summary>
        /// <param name="process"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task WaitForExitAsync(this Process process, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (process.HasExited)
                return Task.CompletedTask;
                
            var tcs = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.TrySetResult(null);
            if(cancellationToken != default(CancellationToken))
                cancellationToken.Register(tcs.SetCanceled);

            return tcs.Task;
        }
    }
}