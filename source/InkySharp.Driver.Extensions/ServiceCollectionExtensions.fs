module InkySharp.Driver.Extensions.ServiceCollection

open System.Device.Gpio
open System.Device.Spi
open System.Runtime.CompilerServices
open InkySharp.Driver.Extensions.DisplayAccess
open InkySharp.Driver.Extensions.DisplayCommunication
open InkySharp.Driver.GpioControllerWrapper
open InkySharp.Driver.InkyImpressionDriver
open InkySharp.Driver.SpiDeviceWrapper
open Microsoft.Extensions.DependencyInjection

// FS0760 : It is recommended that objects supporting the IDisposable interface are created using the syntax 'new Type(args)', rather than 'Type(args)' or 'Type' as a function value representing the constructor, to indicate that resources may be owned by the generated value
// We are allowing this because lifetime is managed by the DI container and the 'new' prefix can't be pipelined
#nowarn "FS0760"

let private addHardwareInterfaces (services : IServiceCollection) =
    services.AddSingleton<IGpioControllerWrapper, GpioControllerWrapper>(fun serviceProvider ->
        GpioController ()
        |> GpioControllerWrapper)
    |> ignore

    services.AddSingleton<ISpiDeviceWrapper, SpiDeviceWrapper>(fun serviceProvider ->
        SpiConnectionSettings (busId = 0)
        |> SpiDevice.Create
        |> SpiDeviceWrapper)
    |> ignore

let private addAccessServices (services : IServiceCollection) =
    services.AddSingleton<DisplayAccessOrchestrator> () |> ignore
    services.AddSingleton<DisplayCommunicationService> () |> ignore

let private addInkyImpression57Mk1 (services : IServiceCollection) (opt : {| IsHorizontalFlipped : bool ; IsVerticalFlipped : bool |}) =
    services |> addHardwareInterfaces
    services |> addAccessServices

    services.AddSingleton<IInkyImpressionWrapper>(fun serviceProvider ->
        let spiDeviceWrapper = serviceProvider.GetRequiredService<ISpiDeviceWrapper> ()
        let gpioControllerWrapper = serviceProvider.GetRequiredService<IGpioControllerWrapper> ()

        InkyImpressionWrapper.Create57MarkI(
             isHorizontalFlipped = opt.IsHorizontalFlipped
            ,isVerticalFlipped = opt.IsVerticalFlipped
            ,spiBus = spiDeviceWrapper
            ,gpio = gpioControllerWrapper))

[<Extension>]
type ServiceCollectionExtensions =
    [<Extension>]
    static member AddInkyImpression57Mk1 (services : IServiceCollection) =
        {| IsHorizontalFlipped = false ; IsVerticalFlipped = false |}
        |> addInkyImpression57Mk1 services

    [<Extension>]
    static member AddInkyImpression57Mk1 (services : IServiceCollection, isHorizontalFlipped, isVerticalFlipped) =
        {| IsHorizontalFlipped = isHorizontalFlipped ; IsVerticalFlipped = isVerticalFlipped |}
        |> addInkyImpression57Mk1 services
