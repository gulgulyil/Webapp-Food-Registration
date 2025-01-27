using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Food_Registration.Controllers;
using Food_Registration.Models;
using Food_Registration.DAL;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Food_Registration.Tests.Controllers
{
  public class ProductControllerTests
  {
    private readonly Mock<IProductRepository> _mockProductRepo;
    private readonly Mock<IProducerRepository> _mockProducerRepo;
    private readonly ProductController _controller;

    private readonly Mock<IWebHostEnvironment> _mockWebHostEnvironment;
    private readonly Mock<ILogger<ProductController>> _mockLogger;


    public ProductControllerTests()
    {
        // Mocking the repository and dependencies
        _mockProductRepo = new Mock<IProductRepository>();
        _mockProducerRepo = new Mock<IProducerRepository>();
        _mockWebHostEnvironment = new Mock<IWebHostEnvironment>();
        _mockLogger = new Mock<ILogger<ProductController>>();

        // Mocking WebRootPath
        _mockWebHostEnvironment.Setup(env => env.WebRootPath).Returns("C:\\fakepath");

        // Initializing the controller with mocked dependencies
        _controller = new ProductController(
            _mockProductRepo.Object,
            _mockProducerRepo.Object,
            _mockWebHostEnvironment.Object,
            _mockLogger.Object
        );

        // Mocking a user for controller context
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] 
        {
            new Claim(ClaimTypes.Name, "test@test.com"),
        }, "mock"));

        _controller.ControllerContext = new ControllerContext()
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        // Setting up TempData
        _controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(),
            Mock.Of<ITempDataProvider>()
        );
    }

    // Helper method to create a mock file
    private IFormFile CreateMockFile(string fileName, string contentType, string content)
    {
        var fileMock = new Mock<IFormFile>();
        var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(contentBytes);

        fileMock.Setup(_ => _.FileName).Returns(fileName);
        fileMock.Setup(_ => _.ContentType).Returns(contentType);
        fileMock.Setup(_ => _.OpenReadStream()).Returns(stream);
        fileMock.Setup(_ => _.Length).Returns(contentBytes.Length);

        return fileMock.Object;
    }

    // Test case 1: Testing the Create method for valid product input
    [Fact]
    public async Task Create_Post_ValidProduct_RedirectsToTable()
    {
        // Arrange: Preparing test data
        var product = new Product
        {
            Name = "Test Product",
            ProducerId = 1,
            NutritionScore = "A"
        };

        var producer = new Producer
        {
            ProducerId = 1,
            OwnerId = "test@test.com"
        };

        var mockFile = CreateMockFile("image.jpg", "image/jpeg", "fake-image-content");

        // Setting up the Producer repository mock
        _mockProducerRepo.Setup(repo => repo.GetProducerByIdAsync(1))
            .ReturnsAsync(producer);

        // Act: Calling the Create method
        var result = await _controller.Create(product, mockFile);

        // Assert: Verifying the redirect and checking the product creation
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Table", redirectResult.ActionName);
        _mockProductRepo.Verify(repo => repo.CreateProductAsync(product), Times.Once);
    }

    // Test case 2: Testing Create method GET request and checking producer list in view
    [Fact]
    public async Task Create_Get_ReturnsViewWithProducerList()
    {
        // Arrange: Creating a list of producers
        var producers = new List<Producer>
        {
            new Producer { ProducerId = 1, Name = "Producer 1", OwnerId = "test@test.com" },
            new Producer { ProducerId = 2, Name = "Producer 2", OwnerId = "test@test.com" }           
        };

        // Setting up the Producer repository mock
        _mockProducerRepo.Setup(repo => repo.GetAllProducersAsync())
            .ReturnsAsync(producers);

        // Act: Calling the Create GET method
        var result = await _controller.Create();

        // Assert: Checking the view and producer list in ViewData
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.NotNull(viewResult.ViewData["Producers"]);
        var producerList = Assert.IsType<SelectList>(viewResult.ViewData["Producers"]);
        Assert.Equal(2, producerList.Count());
    }

    // Test case 3: Testing Create POST method with invalid model and checking for errors
    [Fact]
    public async Task Create_Post_InvalidModel_ReturnsViewWithError()
    {
        // Arrange: Creating an invalid product (empty)
        var product = new Product(); // Empty product
        _controller.ModelState.AddModelError("Name", "Name is required");

        var producers = new List<Producer>
        {
            new Producer { ProducerId = 1, Name = "Producer 1" }
        };

        // Setting up the Producer repository mock
        _mockProducerRepo.Setup(repo => repo.GetAllProducersAsync())
            .ReturnsAsync(producers);

        // Creating a mock file for testing
        var mockFile = CreateMockFile("image.jpg", "image/jpeg", "fake-image-content");

        // Act: Calling the Create POST method
        var result = await _controller.Create(product, mockFile);

        // Assert: Verifying the returned view result
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(_controller.ModelState.IsValid);
    }

    
    // Test case 4: Testing Create POST method with non-existent producer
    [Fact]
    public async Task Create_Post_NullProducer_ReturnsBadRequest()
    {
        // Arrange: Creating a product with a non-existent producer ID
        var product = new Product
        {
          Name = "Test Product",
           ProducerId = 999, // Non-existent producer
          NutritionScore = "A"
        };

        // Mocking the Producer repository to return null for a non-existent producer
        _mockProducerRepo.Setup(repo => repo.GetProducerByIdAsync(999))
            .ReturnsAsync((Producer)null);

        // Act: Calling the Create POST method with a null file
        IFormFile? nullFile = null;
        var result = await _controller.Create(product, nullFile);

        // Assert: Expecting a RedirectResult
        Assert.IsType<RedirectResult>(result); // RedirectResult bekliyoruz
         _mockProductRepo.Verify(repo => repo.CreateProductAsync(It.IsAny<Product>()), Times.Never); // Ürün oluşturma işlemi yapılmamalı
    }

    // Test case 5: Testing Index method to filter products
    [Fact]
    public async Task Index_ReturnsFilteredProducts()
    {
        // Arrange: Preparing a list of products
        var products = new List<Product>
        {
            new Product { ProductId = 1, Name = "Apple", Category = "Fruits" },
            new Product { ProductId = 2, Name = "Orange", Category = "Fruits" },
            new Product { ProductId = 3, Name = "Carrot", Category = "Vegetables" }
        };

        // Mock the GetFilteredProductsAsync to return the filtered list
        _mockProductRepo.Setup(repo => repo.GetFilteredProductsAsync("Apple", "Fruits"))
            .ReturnsAsync(products.Where(p => p.Name == "Apple" && p.Category == "Fruits").ToList());

        // Act: Calling the Index method with filter parameters
        var result = await _controller.Index("Apple", "Fruits");

        // Assert: Verifying the filtered result
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<Product>>(viewResult.Model);

        Assert.Single(model); // Ensure only one product is returned
        Assert.Equal("Apple", model.First().Name); // Verify the correct product
    }

    // Test case 6: Testing Delete method when the product is not found
    [Fact]
    public async Task Delete_ProductAndProducerNotFound_ReturnsNotFound()
    {
        // Arrange: Mocking the Product repository to return null (no product found)
        _mockProductRepo.Setup(repo => repo.GetProductByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((Product)null); 

        // Act: Calling the Delete method with a non-existent product ID
        var result = await _controller.Delete(999);  

        // Assert: Expecting a NotFound result
        var notFoundResult = Assert.IsType<NotFoundResult>(result);  
        _mockProductRepo.Verify(repo => repo.DeleteProductAsync(It.IsAny<int>()), Times.Never);  // Verify DeleteProductAsync is not called
    }

    // Test case 7: Testing Create POST with invalid product (empty name)
    [Fact]
    public async Task Create_Post_InvalidProduct_EmptyName_ReturnsBadRequest()
    {
        // Arrange: Creating a product with an empty name
        var product = new Product { Name = "", ProducerId = 1, NutritionScore = "A" };

        // Mocking the producer repository
        var producer = new Producer { ProducerId = 1, OwnerId = "test@test.com" };
        _mockProducerRepo.Setup(repo => repo.GetProducerByIdAsync(1))
            .ReturnsAsync(producer);

        var mockFile = CreateMockFile("image.jpg", "image/jpeg", "fake-image-content");

        // Adding an error to ModelState
        _controller.ModelState.AddModelError("Name", "Product name is required");

        // Act: Calling the Create method
        var result = await _controller.Create(product, mockFile);

        // Assert: Verifying the method returns a ViewResult
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(_controller.ModelState.IsValid);
    }


    // Test case 8: Testing Update method when the product is not found
    [Fact]
    public async Task Update_ProductNotFound_ReturnsBadRequest()
    {
        // Arrange: Configuring ProductRepository's GetProductByIdAsync method to return null
        _mockProductRepo.Setup(repo => repo.GetProductByIdAsync(It.IsAny<int>()))
           .ReturnsAsync((Product)null); // No product found

        // Act: Calling the Update method with a non-existent ID
        var result = await _controller.Update(999); 

        // Assert: Expecting a BadRequestObjectResult
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Product not found", badRequestResult.Value); 
    }

    
  }
}  