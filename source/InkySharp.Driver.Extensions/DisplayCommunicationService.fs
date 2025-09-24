module InkySharp.Driver.Extensions.DisplayCommunication

open System
open System.Buffers
open System.Diagnostics
open System.Threading
open InkySharp.Driver.Extensions.DisplayAccess
open InkySharp.Driver.InkyGpioWrapper
open InkySharp.Driver.InkyImpressionDriver
open Microsoft.Extensions.Logging
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open SixLabors.ImageSharp.Processing
open SixLabors.ImageSharp.Processing.Processors.Dithering
open SixLabors.ImageSharp.Processing.Processors.Quantization

let private resizeRetainAspectRatio width height (image : Rgba32 Image) =
    image.Mutate (fun context ->
        ResizeOptions(
            Mode = ResizeMode.Max, // Maintain aspect ratio
            Size = Size (width = width, height = height))
        |> context.Resize
        |> ignore)
    image

// TODO: factor this out
let internal displayColorMap =
    let rgb (r : int) (g : int) (b : int) = Rgba32 (byte r, byte g, byte b)
    [
        (rgb 0x0c 0x0c 0x0e, DisplayColor.Black)
        (rgb 0xd2 0xd2 0xd0, DisplayColor.White)
        (rgb 0x1e 0x60 0x1f, DisplayColor.Green)
        (rgb 0x1d 0x1e 0xaa, DisplayColor.Blue)
        (rgb 0x8c 0x1b 0x1d, DisplayColor.Red)
        (rgb 0xd3 0xc9 0x3d, DisplayColor.Yellow)
        (rgb 0xc1 0x71 0x2a, DisplayColor.Orange)
    ]

let private applyDithering (displayColors : Color list) ditherType ditherScale (image : Rgba32 Image) =
    image.Mutate (fun context ->
        let displayColorsAsMem = displayColors |> Seq.toArray |> ReadOnlyMemory
        let options = QuantizerOptions(MaxColors = displayColors.Length, DitherScale = ditherScale, Dither = ditherType)

        PaletteQuantizer(displayColorsAsMem, options)
        |> context.Quantize
        |> ignore)

    image

let internal findClosestColor (colors : (Rgba32 * DisplayColor) seq) (target : Rgba32) =
    let squaredDistance (a : Rgba32) (b : Rgba32) =
        let dr = int b.R - int a.R
        let dg = int b.G - int a.G
        let db = int b.B - int a.B

        dr * dr + dg * dg + db * db

    if Seq.isEmpty colors then failwith "colors cannot be empty" else

    colors
    |> Seq.minBy (fst >> squaredDistance target)
    |> fun (_, displayColor) -> displayColor

let private setPixel (wrapper : IInkyImpressionWrapper) x y displayColor =
    (x, y, displayColor)
    |> wrapper.SetPixel

let private setPixels wrapper displayColorMap (cancellationToken : CancellationToken) width height (image : Rgba32 Image) =
    for x in 0 .. width - 1 do
    for y in 0 .. height - 1 do
        cancellationToken.ThrowIfCancellationRequested ()

        image[x, y]
        |> findClosestColor displayColorMap
        |> setPixel wrapper x y

let private sendImageToDisplay (logger : ILogger)
                               (wrapper : IInkyImpressionWrapper)
                               (displayColors : Color list)
                               (ditherOptions : IDither * float32)
                               (cancellationToken : CancellationToken)
                               (image : Rgba32 Image) = task {
    let width, height = int wrapper.Width, int wrapper.Height
    let ditherType, ditherScale = ditherOptions

    // To be safe, we should always attempt a resize of the image to match the display size.
    // We won't do any rotation/restoring the orientation from the EXIF data here because you should've already done this.
    // We just want to make sure we don't overflow the buffer by using an image too large.
    // TODO: consider just throwing an exception instead of trying to handle this?
    // We may want to allow this as a high-level interface around the display?
    image
    |> resizeRetainAspectRatio width height
    |> applyDithering displayColors ditherType ditherScale
    |> setPixels wrapper displayColorMap cancellationToken width height

    let stopwatch = Stopwatch.StartNew ()
    do! wrapper.Show()
    stopwatch.Stop ()

    logger.LogDebug ("Updated display in {ElapsedSeconds} seconds", stopwatch.Elapsed.TotalSeconds)
}

type DisplayCommunicationService (
     logger : ILogger<DisplayCommunicationService>
    ,inkyImpressionWrapper : IInkyImpressionWrapper
    ,displayAccessOrchestrator : DisplayAccessOrchestrator
) =
    let mutable isDisplayInitialized = false

    let initializeDisplayIfNotInitialized () = task {
        // There are safeguards in the driver to prevent bad things from happening if you try to initialize the display twice.
        // But let's just be extra safe.
        if isDisplayInitialized then return () else

        let initializationStopwatch = Stopwatch.StartNew ()
        do! inkyImpressionWrapper.Initialize ()
        initializationStopwatch.Stop ()

        isDisplayInitialized <- true
        logger.LogDebug("Display initialized in {ElapsedSeconds} sec.", initializationStopwatch.Elapsed.TotalSeconds);
    }

    member _.SetBorderColor (color : DisplayColor , cancellationToken : CancellationToken) = task {
        use! _lock = displayAccessOrchestrator.ObtainExclusiveAccess cancellationToken
        do! initializeDisplayIfNotInitialized ()

        color |> inkyImpressionWrapper.SetBorderColor
    }

    member _.SendImageToDisplay (
         image : Rgba32 Image
        ,ditherType : IDither option
        ,ditherScale : float32 option
        ,cancellationToken
    ) = task {
        use! _lock = displayAccessOrchestrator.ObtainExclusiveAccess cancellationToken
        do! initializeDisplayIfNotInitialized ()

        let ditherType = ditherType |> Option.defaultValue KnownDitherings.FloydSteinberg
        let ditherScale = ditherScale |> Option.defaultValue 0.5f
        let displayColors = displayColorMap |> Seq.map fst |> Seq.map Color |> Seq.toList // FIXME:

        do! image
            |> sendImageToDisplay logger inkyImpressionWrapper displayColors (ditherType, ditherScale) cancellationToken
    }
