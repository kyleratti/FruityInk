using System.Device.Gpio;
using FakeItEasy;
using InkySharp.Driver.GpioControllerWrapper;
using InkySharp.Driver.InkyGpioWrapper;

namespace InkySharp.Tests.DriverTests;

public class GpioHelperTests
{
	private IGpioControllerWrapper _fakeGpio = null!;
	private IGpioHelper _gpioHelper = null!;

	[SetUp]
	public void SetUp()
	{
		_fakeGpio = A.Fake<IGpioControllerWrapper>();
		_gpioHelper = new GpioHelper(gpio: _fakeGpio);
	}

	[TearDown]
	public void TearDown()
	{
		_fakeGpio.Dispose();
	}

	[Test]
	public async Task TestWaitForBusyPin_PinValueHigh()
	{
		A.CallTo(() => _fakeGpio.Read((int)GpioPinName.Busy))
			.Returns(PinValue.High);

		await _gpioHelper.WaitForBusyPin(GpioPinName.Busy, timeout: TimeSpan.Zero);

		A.CallTo(() => _fakeGpio.Read((int)GpioPinName.Busy))
			.MustHaveHappenedOnceExactly();
	}

	[Test]
	public async Task TestWaitForBusyPin_PinValueLow()
	{
		A.CallTo(() => _fakeGpio.Read((int)GpioPinName.Busy))
			.Returns(PinValue.Low).Once()
			.Then.Returns(PinValue.Low).Once()
			.Then.Returns(PinValue.High).Once();

		await _gpioHelper.WaitForBusyPin(GpioPinName.Busy, timeout: TimeSpan.FromSeconds(30));

		A.CallTo(() => _fakeGpio.Read((int)GpioPinName.Busy))
			.MustHaveHappened(3, Times.Exactly);
	}

	[Test]
	public void TestWaitForBusyPin_ThrowsTimeoutExceptionWhenTimedOut()
	{
		A.CallTo(() => _fakeGpio.Read((int)GpioPinName.Busy))
			.Returns(PinValue.Low);

		var ex = Assert.ThrowsAsync<TimeoutException>(async () =>
			await _gpioHelper.WaitForBusyPin(GpioPinName.Busy, timeout: TimeSpan.FromMilliseconds(200)));

		Assert.That(ex, Is.Not.Null);
		Assert.That(ex!.Message, Is.EqualTo("Timeout waiting for busy signal to clear after 0.20 seconds"));
	}
}
