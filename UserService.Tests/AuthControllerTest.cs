using AutoMapper;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UserService.Controllers;
using UserService.Data;
using UserService.Models.Users;

namespace UserService.Tests;

public class AuthControllerTest
{
    
    [Fact]
    public async Task Register_InvalidModel_ReturnsBadRequest()
    {
        // Arrange
        var logger = new Mock<ILogger<AuthController>>();
        var mapper = new Mock<IMapper>();
        var userManager = MockUserManager<ApiUser>();
        var config = new Mock<IConfiguration>();

        var controller = new AuthController(logger.Object, mapper.Object, userManager.Object, config.Object);
        controller.ModelState.AddModelError("Email", "Required"); // робимо модель невалідною

        // Act
        var result = await controller.Register(new UserDto());

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // Допоміжний метод для UserManager
    private static Mock<UserManager<TUser>> MockUserManager<TUser>() where TUser : class
    {
        var store = new Mock<IUserStore<TUser>>();
        return new Mock<UserManager<TUser>>(store.Object, null, null, null, null, null, null, null, null);
    }
}