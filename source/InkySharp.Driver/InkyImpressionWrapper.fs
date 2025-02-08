namespace InkySharp.Driver.InkyImpressionDriver

open System.Device.Gpio
open System.Diagnostics.CodeAnalysis
open System.Threading.Tasks
open InkySharp.Driver
open InkySharp.Driver.InkyGpioWrapper
open InkySharp.Driver.SpiDeviceWrapper
open InkySharp.Driver.GpioControllerWrapper

type DType =
    | UInt8
    | UInt24

module InkyImpression_5_7Colors =
    let private desaturatedColorPalette = [|
        [| 0; 0; 0 |]; // #000000
        [| 255; 255; 255 |];
        [| 0; 255; 0 |];
        [| 0; 0; 255 |];
        [| 255; 0; 0 |];
        [| 255; 255; 0 |];
        [| 255; 140; 0 |];
        [| 255; 255; 255 |]
    |]

    let private saturatedColorPalette = [|
        [| 57; 48; 57 |];
        [| 255; 255; 255 |];
        [| 58; 91; 70 |];
        [| 61; 59; 94 |];
        [| 156; 72; 75 |];
        [| 208; 190; 71 |];
        [| 177; 106; 73 |];
        [| 255; 255; 255 |]
    |]

    [<ExcludeFromCodeCoverage>]
    let paletteBlend (saturation : float) : uint array =
        let dtype = DType.UInt24
        let mutable palette = []

        for i in 0..6 do
            let saturatedColors = Array.map (fun c -> float c * saturation) saturatedColorPalette[i]
            let rs, gs, bs = saturatedColors[0], saturatedColors[1], saturatedColors[2]
            
            let desaturatedColors = Array.map (fun c -> float c * (1.0 - saturation)) desaturatedColorPalette[i]
            let rd, gd, bd = desaturatedColors[0], desaturatedColors[1], desaturatedColors[2]
            
            match dtype with
            | DType.UInt8 -> 
                palette <- palette @ [int (rs + rd); int (gs + gd); int (bs + bd)]
            | DType.UInt24 -> 
                let color = (int(rs + rd) <<< 16) ||| (int(gs + gd) <<< 8) ||| int(bs + bd)
                palette <- palette @ [color]
        
        match dtype with
        | DType.UInt8 -> 
            palette <- palette @ [255; 255; 255]
        | DType.UInt24 -> 
            palette <- palette @ [0xFFFFFF]
            //failwith "No uint24 2"

        palette
        |> List.append [0; 0; 0]
        |> List.map uint
        |> List.toArray

    let getPaletteBlendAsBytes (saturation : float) : (byte * byte * byte) array =
        let mutable palette = []

        for i in 0..6 do // don't include 7/clean; we'll manually add that as pure white later
            let saturatedColors = saturatedColorPalette[i] |> Array.map (fun c -> float c * saturation)
            let rs, gs, bs = saturatedColors[0], saturatedColors[1], saturatedColors[2]

            let desaturatedColors = desaturatedColorPalette[i] |> Array.map (fun c -> float c * (1.0 - saturation))
            let rd, gd, bd = desaturatedColors[0], desaturatedColors[1], desaturatedColors[2]

            palette <- palette @ [byte (rs + rd), byte (gs + gd), byte (bs + bd)]

        palette
        |> Seq.append [255uy, 255uy, 255uy] // this will be index 7/clean
        //|> Seq.append [0uy, 0uy, 0uy] // FIXME: we might not need this
        |> Seq.toArray

    let getPaletteBlendAsUInt (saturation : float) : uint array =
        (*Array.init 7 (fun i ->
            let i = i-1 // For Array.init, i starts at 1, not 0
            let saturatedColors = saturatedColorPalette[i] |> Array.map (fun c -> float c * saturation)
            let rs, gs, bs = saturatedColors[0], saturatedColors[1], saturatedColors[2]

            let desaturatedColors = desaturatedColorPalette[i] |> Array.map (fun c -> float c * (1.0 - saturation))
            let rd, gd, bd = desaturatedColors[0], desaturatedColors[1], desaturatedColors[2]

            let color = (uint(rs + rd) <<< 16) ||| (uint(gs + gd) <<< 8) ||| uint(bs + bd)
            color)
        //|> Seq.append [0xFFFFFFu] // index 7 / white
        //|> Seq.append [0u; 0u; 0u] // FIXME: we might not need this
        |> Seq.toArray*)

        let mutable palette = []

        for i in 0..6 do
            let saturatedColors = saturatedColorPalette[i] |> Array.map (fun c -> float c * saturation)
            let rs, gs, bs = saturatedColors[0], saturatedColors[1], saturatedColors[2]

            let desaturatedColors = desaturatedColorPalette[i] |> Array.map (fun c -> float c * (1.0 - saturation))
            let rd, gd, bd = desaturatedColors[0], desaturatedColors[1], desaturatedColors[2]

            let color = (uint(rs + rd) <<< 16) ||| (uint(gs + gd) <<< 8) ||| uint(bs + bd)
            palette <- palette @ [color]

        palette
        //|> Seq.append [0xFFFFFFu] // index 7 / white
        //|> Seq.append [0u; 0u; 0u] // FIXME: we might not need this
        |> Seq.toArray

type IInkyImpressionWrapper =
    abstract member GetPaletteBlendFromSaturation : saturation : float -> uint array
    abstract member SetPixel : x:int * y:int * color:DisplayColor -> unit
    abstract member GetPixels : unit -> DisplayColor array2d
    abstract member SetBorderColor : DisplayColor -> unit
    abstract member GetBorderColor : unit -> DisplayColor
    abstract member Show : unit -> Task
    abstract member Initialize : unit -> Task
    abstract member Width : uint16
    abstract member Height : uint16
    abstract member AddButtonEventListener : button:DisplayButton * callback:PinChangeEventHandler -> unit
    abstract member RemoveButtonEventListener : button:DisplayButton * callback:PinChangeEventHandler -> unit

(*
let spiConnectionSettings = SpiConnectionSettings (csChannel, 0)
spiConnectionSettings.ClockFrequency <- 3_000_000
// there is no spiBus.no_cs boolean in .NET
spiBus <- SpiDevice.Create(spiConnectionSettings)
*)

type InkyImpressionWrapper internal ( // NOTE: we need the constructor to be internal for unit testing
    isHorizontalFlipped : bool,
    isVerticalFlipped : bool,
    // i2c_bus
    inkyImpressionDriver : IInkyImpressionDriver
) =
    let width : uint16 = uint16 600
    let height : uint16 = uint16 448
    // self.cols, self.rows, self.rotation, self.offset_x, self.offset_y, self.resolution_setting = _RESOLUTION[resolution]
    // _RESOLUTION_5_7_INCH: (_RESOLUTION_5_7_INCH[0], _RESOLUTION_5_7_INCH[1], 0, 0, 0, 0b11),
    let cols = width
    let rows = height
    let rotation = 0
    let offsetX = 0
    let offsetY = 0
    let resolutionAsBinary = 0b11
    let color = "multi"
    let lut = "multi"

    let buffer = Array2D.init (int rows) (int cols) (fun _ _ -> 0uy)

    let csPin = GpioPinName.Cs0
    let csChannel =
        match int csPin with
        | x when int x = (int GpioPinName.Cs0) -> int GpioPinName.Cs0
        | 7 (* FIXME: magic number, not sure what this is *) -> 7
        | _ -> 0

    [<ExcludeFromCodeCoverage>]
    static member Create (
        isHorizontalFlipped : bool,
        isVerticalFlipped : bool,
        spiBus : ISpiDeviceWrapper,
        gpio : IGpioControllerWrapper
    ) =
        let inkyImpressionDriver = InkyImpressionDriver(
                csPin = GpioPinName.Cs0,
                dcPin = GpioPinName.Dc,
                resetPin = GpioPinName.Reset,
                busyPin = GpioPinName.Busy,
                spiBus = spiBus,
                gpio = gpio,
                gpioHelper = GpioHelper(gpio)
            )
        InkyImpressionWrapper (isHorizontalFlipped, isVerticalFlipped, inkyImpressionDriver)

    [<ExcludeFromCodeCoverage>]
    member this.Initialize () = task {
        do! inkyImpressionDriver.Initialize ()
    }

    member this.SetBorderColor (color : DisplayColor) = inkyImpressionDriver.SetBorderColor color
    member this.GetBorderColor () = inkyImpressionDriver.GetBorderColor ()

    member this.Width = width
    member this.Height = height

    member this.GetColorPaletteFromSaturation (saturation : float) =
        InkyImpression_5_7Colors.getPaletteBlendAsUInt saturation

    member this.SetPixel (x : int, y : int, color : DisplayColor) =
            let maxCols = int cols
            let maxRows = int rows

            if x < 0 || x >= maxCols then
                failwithf "x must be >= 0 and < %d, but was %d" maxCols x
            else if y < 0 || y >= maxRows then
                failwithf "y must be >= 0 and < %d, but was %d" maxRows y

            let colorAsByte = color.ToByte ()
            buffer[y,x] <- colorAsByte &&& 0x07uy

        member this.GetPixels () =
            buffer |> Array2D.mapi (fun _ _ byte -> DisplayColor.FromByte byte)

        member this.Show () = task {
            let conditionalMap (condition : bool) (f : 'a -> 'a) (x : 'a) =
                if condition then f x
                else x

            let buf =
                buffer
                |> Array2D.copy
                |> conditionalMap isVerticalFlipped Array2DUtil.flipLeftRight
                |> conditionalMap isHorizontalFlipped Array2DUtil.flipUpsideDown
                |> conditionalMap (rotation <> 0) (Array2DUtil.rotate rotation)
                |> Array2DUtil.flatten

            // TODO: I'd like to understand what this is doing, factor it out, and put unit tests on it
            let resultBuf =
                buf
                |> Seq.chunkBySize 2
                |> Seq.map (fun pair ->
                    ((pair.[0] <<< 4) &&& 0xF0uy) ||| (pair.[1] &&& 0x0Fuy)
                )
                |> Seq.toArray


            (*let buf1 = Array.init (buf.Length/2) (fun i -> (buf[2*i] <<< 4) &&& 0xF0uy)
            let buf2 = Array.init (buf.Length/2) (fun i -> buf[2*i+1] &&& 0x0Fuy)
            let resultBuf = Array.init (buf.Length/2) (fun i -> buf1[i] ||| buf2[i])*)

            do! inkyImpressionDriver.FlushBufferToDisplay resultBuf
        }

    member this.AddButtonEventListener (button : DisplayButton, callback : PinChangeEventHandler) =
        inkyImpressionDriver.AddButtonEventListener (button, callback)

    member this.RemoveButtonEventListener (button : DisplayButton, callback : PinChangeEventHandler) =
        inkyImpressionDriver.RemoveButtonEventListener (button, callback)

    interface IInkyImpressionWrapper with
        member this.SetBorderColor (color : DisplayColor) = this.SetBorderColor color
        member this.GetBorderColor () = this.GetBorderColor ()
        member this.Width = width
        member this.Height = height
        member this.SetPixel (x : int, y : int, color : DisplayColor) = this.SetPixel (x, y, color)
        member this.GetPaletteBlendFromSaturation (saturation : float) = this.GetColorPaletteFromSaturation (saturation)
        member this.GetPixels () = this.GetPixels ()
        member this.Show () = this.Show ()
        member this.Initialize () = this.Initialize ()
        member this.AddButtonEventListener (button : DisplayButton, callback : PinChangeEventHandler) = this.AddButtonEventListener (button, callback)
        member this.RemoveButtonEventListener (button : DisplayButton, callback : PinChangeEventHandler) = this.RemoveButtonEventListener (button, callback)
