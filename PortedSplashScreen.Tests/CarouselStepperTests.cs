using PortedSplashScreen.Services;

namespace PortedSplashScreen.Tests;

public class CarouselStepperTests
{
    [Fact]
    public void Move_AdvancesAndWrapsForward()
    {
        Assert.Equal(1, CarouselStepper.Move(0, 3, 1));
        Assert.Equal(0, CarouselStepper.Move(2, 3, 1));
    }

    [Fact]
    public void Move_WrapsBackward()
    {
        Assert.Equal(2, CarouselStepper.Move(0, 3, -1));
        Assert.Equal(1, CarouselStepper.Move(2, 3, -1));
    }

    [Fact]
    public void Move_WithoutDeltaOnlyNormalizesCurrentIndex()
    {
        Assert.Equal(0, CarouselStepper.Move(0, 3, 0));
        Assert.Equal(2, CarouselStepper.Move(-1, 3, 0));
        Assert.Equal(4, CarouselStepper.Move(9, 5, 0));
    }

    [Fact]
    public void Move_ReturnsMinusOneWhenThereIsNoImage()
    {
        Assert.Equal(-1, CarouselStepper.Move(0, 0, 1));
        Assert.Equal(-1, CarouselStepper.Move(2, -3, -1));
    }
}
