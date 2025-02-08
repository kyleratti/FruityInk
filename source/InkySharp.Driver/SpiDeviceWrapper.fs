namespace InkySharp.Driver.SpiDeviceWrapper

open System
open System.Device.Spi
open System.Diagnostics.CodeAnalysis

type ISpiDeviceWrapper =
    // I am normally against using IDisposable in interfaces, but in this case it's necessary because we are wrapping the SpiDevice class.
    // The only reason we actually need this wrapper is because of the Write(ReadOnlySpan<byte>) method in the SpiDevice class causing problems with unit testing.
    // If/when that bug is fixed, we can remove this wrapper and just use SpiDevice directly.
    inherit IDisposable
    abstract member ConnectionSettings : SpiConnectionSettings with get
    abstract member Read : buffer : byte array -> unit
    abstract member ReadByte : unit -> byte
    abstract member TransferFullDuplex : writeBuffer : byte array * readBuffer : byte array -> unit
    // NOTE: The Write method here is a byte array instead of a ReadOnlySpan<byte> because of a crazy bug I ran into when unit testing.
    // When I used a ReadOnlySpan<byte> here, the unit test would fail with an InvalidProgramException when the
    // mocking libraries tried to proxy this call. I have no idea why.
    // Luckily there's an implicit conversion from byte array to ReadOnlySpan<byte> so it's not a big deal. But it's still weird.
    abstract member Write : buffer : byte array -> unit
    abstract member WriteByte : value : byte -> unit

[<ExcludeFromCodeCoverage>]
type SpiDeviceWrapper (spiDevice : SpiDevice) =
    member _.ConnectionSettings with get() = spiDevice.ConnectionSettings
    member _.Read (buffer : byte array) = spiDevice.Read buffer
    member _.ReadByte () = spiDevice.ReadByte ()
    member _.TransferFullDuplex (writeBuffer : byte array, readBuffer : byte array) =
        spiDevice.TransferFullDuplex (writeBuffer, readBuffer)
    member _.Write (buffer : byte array) = spiDevice.Write buffer
    member _.WriteByte (value : byte) = spiDevice.WriteByte value
    member _.Dispose () = spiDevice.Dispose ()

    interface ISpiDeviceWrapper with
        member this.ConnectionSettings with get () = this.ConnectionSettings
        member this.Read (buffer : byte array) = this.Read buffer
        member this.ReadByte () = this.ReadByte ()
        member this.TransferFullDuplex (writeBuffer : byte array, readBuffer : byte array) =
            this.TransferFullDuplex (writeBuffer, readBuffer)
        member this.Write (buffer : byte array) = this.Write buffer
        member this.WriteByte (value : byte) = this.WriteByte value
        member this.Dispose () = this.Dispose ()
