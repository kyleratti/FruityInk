namespace InkySharp.Driver.GpioControllerWrapper

open System
open System.Device.Gpio
open System.Diagnostics.CodeAnalysis
open System.Threading
open System.Threading.Tasks

type IGpioControllerWrapper =
    inherit IDisposable
    abstract member PinCount : int with get
    abstract member IsPinOpen : pinNumber:int -> bool
    abstract member OpenPin : pinNumber : int -> GpioPin
    abstract member OpenPin : pinNumber : int * mode : PinMode -> GpioPin
    abstract member OpenPin : pinNumber : int * mode : PinMode * initialValue : PinValue -> GpioPin
    abstract member ClosePin : pinNumber:int -> unit
    abstract member IsPinModeSupported : pinNumber:int * mode:PinMode -> bool
    abstract member GetPinMode : pinNumber:int -> PinMode
    abstract member SetPinMode : pinNumber:int * mode:PinMode -> unit
    abstract member Read : pinNumber:int -> PinValue
    abstract member Write : pinNumber:int * value:PinValue -> unit
    abstract member WaitForEvent : pinNumber:int * eventTypes:PinEventTypes * cancellationToken : CancellationToken -> WaitForEventResult
    abstract member WaitForEventAsync : pinNumber:int * eventTypes:PinEventTypes * cancellationToken : CancellationToken -> ValueTask<WaitForEventResult>
    abstract member RegisterCallbackForPinValueChangedEvent : pinNumber:int * eventType:PinEventTypes * callback:PinChangeEventHandler -> unit
    abstract member UnregisterCallbackForPinValueChangedEvent : pinNumber:int * callback:PinChangeEventHandler -> unit
    
[<ExcludeFromCodeCoverage>]
type GpioControllerWrapper (controller : GpioController) =
    interface IGpioControllerWrapper with
        member _.PinCount = controller.PinCount
        member _.IsPinOpen (pinNumber : int) = controller.IsPinOpen pinNumber
        member _.OpenPin (pinNumber : int) = controller.OpenPin pinNumber
        member _.OpenPin (pinNumber : int, mode : PinMode) = controller.OpenPin (pinNumber, mode)
        member _.OpenPin (pinNumber : int, mode : PinMode, initialValue : PinValue) = controller.OpenPin (pinNumber, mode, initialValue)
        member _.ClosePin (pinNumber : int) = controller.ClosePin pinNumber
        member _.Dispose () = controller.Dispose ()
        member _.IsPinModeSupported (pinNumber : int, mode : PinMode) = controller.IsPinModeSupported (pinNumber, mode)
        member _.GetPinMode (pinNumber : int) = controller.GetPinMode pinNumber
        member _.SetPinMode (pinNumber : int, mode : PinMode) = controller.SetPinMode (pinNumber, mode)
        member _.Read (pinNumber : int) = controller.Read pinNumber
        member _.Write (pinNumber : int, value : PinValue) = controller.Write (pinNumber, value)
        member _.WaitForEvent (pinNumber : int, eventTypes : PinEventTypes, cancellationToken : CancellationToken) =
            controller.WaitForEvent (pinNumber, eventTypes, cancellationToken)
        member _.WaitForEventAsync (pinNumber : int, eventTypes : PinEventTypes, cancellationToken : CancellationToken) =
            controller.WaitForEventAsync (pinNumber, eventTypes, cancellationToken)
        member _.RegisterCallbackForPinValueChangedEvent (pinNumber : int, eventType : PinEventTypes, callback : PinChangeEventHandler) = controller.RegisterCallbackForPinValueChangedEvent (pinNumber, eventType, callback)
        member _.UnregisterCallbackForPinValueChangedEvent (pinNumber : int, callback : PinChangeEventHandler) = controller.UnregisterCallbackForPinValueChangedEvent (pinNumber, callback)
