using InkySharp.Driver;

namespace InkySharp.Tests.DriverTests;

public class Array2DUtilTests
{
    [Test]
    public void TestFlatten()
    {
        
        // Arrange
        var array2D = new[,] { {1, 2, 3}, {4, 5, 6}, {7, 8, 9} };
        var expected = new[] {1, 2, 3, 4, 5, 6, 7, 8, 9};

        // Act
        var actual = Array2DUtil.flatten(array2D);

        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void TestFlipLeftRight()
    {
        // Arrange
        var array2D = new[,] { { 1, 2 }, { 3, 4 } };
        var expected = new[,] { { 2, 1 }, { 4, 3 } };

        // Act
        var actual = Array2DUtil.flipLeftRight(array2D);

        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void TestFlipUpsideDown()
    {
        // Arrange
        var array2D = new[,] { { 1, 2 }, { 3, 4 } };
        var expected = new[,] { { 3, 4 }, { 1, 2 } };
    
        // Act
        var actual = Array2DUtil.flipUpsideDown(array2D);
    
        // Assert
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void TestRotateThrowsException()
    {
        // Arrange
        var array2D = new[,] { { 1, 2, 3 }, { 4, 5, 6 }, { 7, 8, 9 } };
        //var expected = new[,] { { 3, 6, 9 }, { 2, 5, 8 }, { 1, 4, 7 } };
    
        // Act
        var ex = Assert.Throws<Exception>(() => Array2DUtil.rotate(rotation: 1, array2D));
    
        // Assert
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Is.EqualTo("Rotation not supported yet"));
    }
}
