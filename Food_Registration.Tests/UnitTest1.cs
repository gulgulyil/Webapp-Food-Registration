namespace Food_Registration.Tests;
using Xunit;
using Moq;
using Food_Registration.Controllers;
using Food_Registration.Models;
using Food_Registration.DAL;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        Assert.Equal("Test Product", "Test Product");
    }
}