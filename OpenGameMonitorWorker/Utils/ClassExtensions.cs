//using JKang.IpcServiceFramework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenGameMonitorWorker.Utils
{
    static class ClassExtensions
    {
        /*
        public static async Task RunAsync(this IIpcServiceHost proc, CancellationToken cancellationToken = default(CancellationToken))
        {
            // We create a TaskCompletionSource of decimal
            var taskCompletionSource = new TaskCompletionSource<void>();

            // Registering a lambda into the cancellationToken
            cancellationToken.Register(() =>
            {
                // We received a cancellation message, cancel the TaskCompletionSource.Task
                taskCompletionSource.TrySetCanceled();
            });

            var task = proc.RunAsync();

            // Wait for the first task to finish among the two
            var completedTask = await Task.WhenAny(task, taskCompletionSource.Task);

            // If the completed task is our long running operation we set its result.
            if (completedTask == task)
            {
                // Extract the result, the task is finished and the await will return immediately
                var result = await task;

                // Set the taskCompletionSource result
                taskCompletionSource.TrySetResult(result);
            }

            // Return the result of the TaskCompletionSource.Task
            return await taskCompletionSource.Task;
        }
        */
    }
}
