module InkySharp.Driver.Extensions.DisplayAccess

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Nito.AsyncEx

type DisplayAccessOrchestrator (
    logger : ILogger<DisplayAccessOrchestrator>
) =
    let exclusiveAccessLock = AsyncLock ()

    member _.ObtainExclusiveAccess (cancellationToken : CancellationToken)  = task {
        return! exclusiveAccessLock.LockAsync cancellationToken
    }

    member this.WithExclusiveAccess (action : Func<CancellationToken, Task>, cancellationToken : CancellationToken) = task {
        logger.LogDebug("📺 Waiting for exclusive access to display.")

        use! _accessLock = this.ObtainExclusiveAccess cancellationToken
        logger.LogDebug("📺 Obtained exclusive access to display.")

        do! action.Invoke cancellationToken
        logger.LogDebug("📺 Released exclusive access to display.");
    }
