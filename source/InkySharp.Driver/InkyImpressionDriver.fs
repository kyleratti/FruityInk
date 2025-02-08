namespace InkySharp.Driver.InkyGpioWrapper

open System
open System.Device.Gpio
open System.Diagnostics
open System.Threading.Tasks
open InkySharp.Driver.GpioControllerWrapper
open InkySharp.Driver.SpiDeviceWrapper

[<RequireQualifiedAccess>]
type DisplayButton =
    | ButtonOne
    | ButtonTwo
    | ButtonThree
    | ButtonFour

[<RequireQualifiedAccess>]
type GpioPinName =
    | Reset = 27
    | Busy = 17
    | Dc = 22
    | Mosi = 10
    | Sclk = 11
    | Cs0 = 8
    | ButtonOne = 5 // FIXME: this probably has a proper pin name
    | ButtonTwo = 6 // FIXME: this probably has a proper pin name
    | ButtonThree = 16 // FIXME: this probably has a proper pin name
    | ButtonFour = 24 // FIXME: this probably has a proper pin name

[<RequireQualifiedAccess>]
[<Struct>]
type DisplayColor =
    | Black
    | White
    | Green
    | Blue
    | Red
    | Yellow
    | Orange
    | Clean
    member this.Merge (
        black : Func<'a>,
        white : Func<'a>,
        green : Func<'a>,
        blue : Func<'a>,
        red : Func<'a>,
        yellow : Func<'a>,
        orange : Func<'a>,
        clean : Func<'a>
    ) =
        match this with
        | Black -> black.Invoke()
        | White -> white.Invoke()
        | Green -> green.Invoke()
        | Blue -> blue.Invoke()
        | Red -> red.Invoke()
        | Yellow -> yellow.Invoke()
        | Orange -> orange.Invoke()
        | Clean -> clean.Invoke()
    member this.ToByte () =
        match this with
        | Black -> 0uy
        | White -> 1uy
        | Green -> 2uy
        | Blue -> 3uy
        | Red -> 4uy
        | Yellow -> 5uy
        | Orange -> 6uy
        | Clean -> 7uy
    static member FromByte (value : byte) : DisplayColor =
        match value with
        | 0uy -> DisplayColor.Black
        | 1uy -> DisplayColor.White
        | 2uy -> DisplayColor.Green
        | 3uy -> DisplayColor.Blue
        | 4uy -> DisplayColor.Red
        | 5uy -> DisplayColor.Yellow
        | 6uy -> DisplayColor.Orange
        | 7uy -> DisplayColor.Clean
        | value -> failwithf "Invalid byte value for DisplayColor: %A" value

type UCBytes =
    | UC8159_PSR = 0x00uy
    | UC8159_PWR = 0x01uy
    | UC8159_POF = 0x02uy
    | UC8159_PFS = 0x03uy
    | UC8159_PON = 0x04uy
    | UC8159_BTST = 0x06uy
    | UC8159_DSLP = 0x07uy
    | UC8159_DTM1 = 0x10uy
    | UC8159_DSP = 0x11uy
    | UC8159_DRF = 0x12uy
    | UC8159_IPC = 0x13uy
    | UC8159_PLL = 0x30uy
    | UC8159_TSC = 0x40uy
    | UC8159_TSE = 0x41uy
    | UC8159_TSW = 0x42uy
    | UC8159_TSR = 0x43uy
    | UC8159_CDI = 0x50uy
    | UC8159_LPD = 0x51uy
    | UC8159_TCON = 0x60uy
    | UC8159_TRES = 0x61uy
    | UC8159_DAM = 0x65uy
    | UC8159_REV = 0x70uy
    | UC8159_FLG = 0x71uy
    | UC8159_AMV = 0x80uy
    | UC8159_VV = 0x81uy
    | UC8159_VDCS = 0x82uy
    | UC8159_PWS = 0xE3uy
    | UC8159_TSSET = 0xE5uy

type private PanelSetting =
    | ResolutionSelect = 0b11000000 // Resolution select, 0b00 = 640x480, our panel is 0b11 = 600x448
    | LutSelection = 0b00100000 // LUT selection, 0 = ext flash, 1 = registers, we use ext flash
    | Ignore = 0b00010000
    | GateScanDirection = 0b00001000 // Gate scan direction, 0 = down, 1 = up (default)
    | SourceShiftDirection = 0b00000100 // Source shift direction, 0 = left, 1 = right (default)
    | DcDcConverter = 0b00000010 // DC-DC converter, 0 = off, 1 = on
    | SoftReset = 0b00000001 // Soft reset, 0 = Reset, 1 = Normal (Default)
    | Res600x448 = 0b11 // 600x448
    | Res640x400 = 0b10 // 640x400

module private Spi =
    let chunkSize = 4096
    let command = 0
    let data = 1

type private DType =
    | UInt8
    | UInt24

// I hate having a stub helper interface like this, but 
type IGpioHelper =
    abstract member WaitForBusyPin : pin:GpioPinName * timeout:TimeSpan -> Task

type GpioHelper (gpio : IGpioControllerWrapper) =
    interface IGpioHelper with
        member this.WaitForBusyPin(pin : GpioPinName, timeout : TimeSpan) = task { // FIXME: this should take a cancellation token
            // If the busy_pin is *high* (pulled up by host)
            // then assume we're not getting a signal from inky
            // and wait the timeout period to be safe.
            if gpio.Read(int pin) = PinValue.High then
                // FIXME: this should log
                do! Task.Delay timeout
                ()
            else
                // If the busy_pin is *low* (pulled down by inky)
                // then wait for it to high.
                let startTime = Stopwatch.StartNew ()
                while gpio.Read(int pin) = PinValue.Low do
                    do! Task.Delay (TimeSpan.FromMilliseconds 10)
                    let elapsed = startTime.Elapsed
                    if elapsed > timeout then
                        // FIXME: this should be a logger
                        raise (TimeoutException (sprintf "Timeout waiting for busy signal to clear after %.2f seconds" timeout.TotalSeconds))
                    else ()        
        }

type IInkyImpressionDriver =
    abstract member IsInitialized : bool with get
    abstract member InitGpio : unit -> Task
    abstract member ResetResolutionSetting : unit -> unit
    abstract member ResetPanelSettings : unit -> unit
    abstract member ResetPowerSettings : unit -> unit
    abstract member ResetSetPllClockFrequency : unit -> unit
    abstract member ResetSendTseRegisterToDisplay : unit -> unit
    abstract member ResetVcomAndDataIntervalSetting : unit -> unit
    abstract member ResetGateSourceNonOverlapPeriod : unit -> unit
    abstract member ResetDisableExternalFlash : unit -> unit
    abstract member ResetUC8159_7C : unit -> unit
    abstract member ResetPowerOffSequence : unit -> unit
    abstract member Initialize : unit -> Task
    abstract member FlushBufferToDisplay : buffer:byte array -> Task
    abstract member ResetDisplay : unit -> unit
    abstract member SpiWrite : dc:int * values: byte array -> unit
    abstract member SetBorderColor : color:DisplayColor -> unit
    abstract member GetBorderColor : unit -> DisplayColor
    abstract member AddButtonEventListener : pin:DisplayButton * callback:PinChangeEventHandler -> unit
    abstract member RemoveButtonEventListener : pin:DisplayButton * callback:PinChangeEventHandler -> unit

module internal PinHelper =
    let mapButtonToPin = function
        | DisplayButton.ButtonOne -> GpioPinName.ButtonOne
        | DisplayButton.ButtonTwo -> GpioPinName.ButtonTwo
        | DisplayButton.ButtonThree -> GpioPinName.ButtonThree
        | DisplayButton.ButtonFour -> GpioPinName.ButtonFour

type internal InkyImpressionDriver (
    csPin : GpioPinName,
    dcPin : GpioPinName,
    resetPin : GpioPinName,
    busyPin : GpioPinName,
    spiBus : ISpiDeviceWrapper,
    gpio : IGpioControllerWrapper,
    gpioHelper : IGpioHelper
) =
    let mutable isInitialized = false
    let mutable borderColor = DisplayColor.White
    let width : uint16 = uint16 600
    let height : uint16 = uint16 448
    let resolutionAsBinary = 0b11

    let spiWrite (dc : int) (values : byte array) =
        // Interestingly enough, the FlexLabs.Inky.InkyDriver doesn't seem to set the CS pin?
        // We're mirroring Inky's Python code here, so we'll keep setting it.
        gpio.Write(int csPin, PinValue.Low)
        gpio.Write(int dcPin, PinValue.op_Implicit(dc))

        values
        |> Seq.chunkBySize Spi.chunkSize
        |> Seq.iter spiBus.Write

        gpio.Write(int csPin, PinValue.High)

    let sendData (data : byte array) =
        data |> spiWrite Spi.data

    let sendCommand (command : UCBytes) =
        spiWrite Spi.command [|byte command|]

    let sendCommandWithData (command : UCBytes) (data : byte array) =
        sendCommand command
        sendData data

    let packUInt16ToBytes (val1 : uint16) (val2 : uint16) =
        let bytes1 = BitConverter.GetBytes val1
        let bytes2 = BitConverter.GetBytes val2
        Array.append bytes1 bytes2

    interface IInkyImpressionDriver with
        member this.AddButtonEventListener (button : DisplayButton, callback : PinChangeEventHandler) =
            let pin = button |> PinHelper.mapButtonToPin
            gpio.OpenPin (int pin, PinMode.InputPullUp) |> ignore
            gpio.SetPinMode (int pin, PinMode.InputPullUp)
            gpio.RegisterCallbackForPinValueChangedEvent (int pin, PinEventTypes.Falling, callback)

        member this.RemoveButtonEventListener (button : DisplayButton, callback : PinChangeEventHandler) =
            let pin = button |> PinHelper.mapButtonToPin
            if gpio.IsPinOpen (int pin) then
                gpio.UnregisterCallbackForPinValueChangedEvent (int pin, callback)
                gpio.ClosePin (int pin)

        member this.IsInitialized = isInitialized

        member _.ResetResolutionSetting () =
            // Resolution Setting
            // 10bit horizontal followed by a 10bit vertical resolution
            // we'll let struct.pack do the work here and send 16bit values
            // life is too short for manual bit wrangling
            (packUInt16ToBytes width height)
            |> sendCommandWithData UCBytes.UC8159_TRES

        member _.ResetPanelSettings () =
            // Panel Setting
            // 0b11000000 = Resolution select, 0b00 = 640x480, our panel is 0b11 = 600x448
            // 0b00100000 = LUT selection, 0 = ext flash, 1 = registers, we use ext flash
            // 0b00010000 = Ignore
            // 0b00001000 = Gate scan direction, 0 = down, 1 = up (default)
            // 0b00000100 = Source shift direction, 0 = left, 1 = right (default)
            // 0b00000010 = DC-DC converter, 0 = off, 1 = on
            // 0b00000001 = Soft reset, 0 = Reset, 1 = Normal (Default)
            // 0b11 = 600x448
            // 0b10 = 640x400
            [|
                ((byte resolutionAsBinary <<< 6) ||| byte 0b101111) // See above for more magic numbers
                byte 0x08                                     // display_colours == UC8159_7C
            |]
            //|> Array.collect BitConverter.GetBytes // FIXME: is this correct?
            |> sendCommandWithData UCBytes.UC8159_PSR

        member _.ResetPowerSettings () =
            [|
                (byte 0x06 <<< 3) |||  // ??? - not documented in UC8159 datasheet  # noqa: W504
                (byte 0x01 <<< 2) |||  // SOURCE_INTERNAL_DC_DC                     # noqa: W504
                (byte 0x01 <<< 1) |||  // GATE_INTERNAL_DC_DC                       # noqa: W504
                byte 0x01; // LV_SOURCE_INTERNAL_DC_DC
                byte 0x00; // VGx_20V
                byte 0x23; // UC8159_7C
                byte 0x23; // UC8159_7C
            |]
            |> sendCommandWithData UCBytes.UC8159_PWR

        member _.ResetSetPllClockFrequency () =
            // Set the PLL clock frequency to 50Hz
            // 0b11000000 = Ignore
            // 0b00111000 = M
            // 0b00000111 = N
            // PLL = 2MHz * (M / N)
            // PLL = 2MHz * (7 / 4)
            // PLL = 2,800,000 ???
            [|byte 0x3C|]
            |> sendCommandWithData UCBytes.UC8159_PLL

        member _.ResetSendTseRegisterToDisplay () =
            [|byte 0x00|]
            |> sendCommandWithData UCBytes.UC8159_TSE

        member _.ResetVcomAndDataIntervalSetting () =
            // 0b11100000 = Vborder control (0b001 = LUTB voltage)
            // 0b00010000 = Data polarity
            // 0b00001111 = Vcom and data interval (0b0111 = 10, default)
            let borderColorByte = borderColor.ToByte ()
            [|byte ((borderColorByte <<< 5) ||| byte 0x17)|]
            |> sendCommandWithData UCBytes.UC8159_CDI // 0b00110111

        member _.ResetGateSourceNonOverlapPeriod () =
            // Gate/Source non-overlap period
            // 0b11110000 = Source to Gate (0b0010 = 12nS, default)
            // 0b00001111 = Gate to Source
            [|byte 0x22|]
            |> sendCommandWithData UCBytes.UC8159_TCON // 0b00100010

        member _.ResetDisableExternalFlash () =
            [|byte 0x00|]
            |> sendCommandWithData UCBytes.UC8159_DAM

        member _.ResetUC8159_7C () =
            [|byte 0xAA|]
            |> sendCommandWithData UCBytes.UC8159_PWS

        member _.ResetPowerOffSequence () =
            // Power off sequence
            // 0b00110000 = power off sequence of VDH and VDL, 0b00 = 1 frame (default)
            // All other bits ignored?
            [|byte 0x00|]
            |> sendCommandWithData UCBytes.UC8159_PFS // PFS_1_Frame

        member this.SpiWrite (dc : int, values : byte array) =
            gpio.Write(int csPin, PinValue.Low)
            gpio.Write(int dcPin, PinValue.op_Implicit(dc))

            if Seq.isEmpty values then failwith "Cannot write empty data to SPI bus" else

            // Write the data in chunks to avoid overflowing the SPI buffer
            values
            |> Seq.chunkBySize Spi.chunkSize
            |> Seq.iter spiBus.Write

            gpio.Write(int csPin, PinValue.High)

        member this.InitGpio () = task {
            let initializePin (mode : PinMode) (initial : PinValue option) (pin : GpioPinName) =
                gpio.OpenPin (int pin, mode) |> ignore
                gpio.SetPinMode (int pin, mode)
                if Option.isSome initial then
                    gpio.Write (int pin, Option.get initial)
                else ()
                    
            if isInitialized then () else
            // gpio.setmode(self.gpio.BCM) // SEE: https://raspberrypi.stackexchange.com/a/12967
            // gpio.setwarnings(False)
            csPin |> initializePin PinMode.Output (Some PinValue.High)
            dcPin |> initializePin PinMode.Output (Some PinValue.Low)
            resetPin |> initializePin PinMode.Output (Some PinValue.High)
            busyPin |> initializePin PinMode.Input None

            isInitialized <- true

            gpio.Write(int resetPin, PinValue.Low)
            do! Task.Delay (TimeSpan.FromMilliseconds 100)
            gpio.Write(int resetPin, PinValue.High)

#if NET_9_0_OR_GREATER
            let timespan = TimeSpan.FromSeconds 40L
#else
            let timespan = TimeSpan.FromSeconds (float 40)
#endif

            do! gpioHelper.WaitForBusyPin (busyPin, timespan)
        }

        member this.SetBorderColor (color : DisplayColor) = borderColor <- color

        member this.GetBorderColor () = borderColor

        // FIXME: what is this actually? it's the _update function from the Python driver but it isn't updating anything...show does that
        member this.FlushBufferToDisplay (buffer : byte array) = task {
            let this = this :> IInkyImpressionDriver

            this.ResetDisplay ()

            let data = buffer// |> ArrayHelper.convert2DArrayTo1D

            sendCommandWithData UCBytes.UC8159_DTM1 data

            sendCommand UCBytes.UC8159_PON
            do! gpioHelper.WaitForBusyPin (busyPin, (TimeSpan.FromMilliseconds 210))

            sendCommand UCBytes.UC8159_DRF
#if NET_9_0_OR_GREATER
            let timespan = TimeSpan.FromSeconds 32
#else
            let timespan = TimeSpan.FromSeconds (float 32)
#endif
            do! gpioHelper.WaitForBusyPin (busyPin, timespan)

            sendCommand UCBytes.UC8159_POF
            do! gpioHelper.WaitForBusyPin (busyPin, (TimeSpan.FromMilliseconds 200))
        }

        member this.ResetDisplay () =
            let this = this :> IInkyImpressionDriver

            if not this.IsInitialized then
                failwith "You must initialize the driver before resetting the display"
            else

            this.ResetResolutionSetting ()
            this.ResetPanelSettings ()
            this.ResetPowerSettings ()
            this.ResetSetPllClockFrequency ()
            this.ResetSendTseRegisterToDisplay ()
            this.ResetVcomAndDataIntervalSetting ()
            this.ResetGateSourceNonOverlapPeriod ()
            this.ResetDisableExternalFlash ()
            this.ResetUC8159_7C ()
            this.ResetPowerOffSequence ()

        member this.Initialize () = task {
            let this = this :> IInkyImpressionDriver
            do! this.InitGpio ()

            this.ResetDisplay ()
        }
