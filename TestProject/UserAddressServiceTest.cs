using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using TBD.AddressModule.Data;
using TBD.AddressModule.Exceptions;
using TBD.AddressModule.Models;
using TBD.AddressModule.Services;
using TBD.API.DTOs.Users;
using TBD.UserModule.Models;
using TBD.UserModule.Services;

namespace TBD.TestProject;

[TestFixture]
public class UserAddressServiceTests
{
    private AddressDbContext _context;
    private Mock<IMapper> _mockMapper;
    private Mock<IUserService> _mockUserService;
    private UserAddressService _userAddressService;
    private List<UserAddress> _testAddresses;
    private User? _testUser;

    [SetUp]
    public async Task Setup()
    {
        // Create an in-memory database with unique name per test
        var options = new DbContextOptionsBuilder<AddressDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AddressDbContext(options);

        // Create test user with all required properties
        _testUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "testuser@example.com",
            Password = "testpassword123",
            Username = "testuser",
            Schedule = null
        };

        // Add the user to the context first
        await _context.Set<User>().AddAsync(_testUser);
        await _context.SaveChangesAsync();

        // Create test addresses
        _testAddresses = new List<UserAddress>
        {
            new UserAddress(_testUser.Id, _testUser, "123 Main St", null, "New York", "NY", "10001")
            {
                Id = Guid.NewGuid()
            },
            new UserAddress(_testUser.Id, _testUser, "456 Oak Ave", "Apt 2", "Los Angeles", "CA", "90210")
            {
                Id = Guid.NewGuid()
            },
            new UserAddress(_testUser.Id, _testUser, "789 Pine Rd", null, "New York", "NY", "10002")
            {
                Id = Guid.NewGuid()
            }
        };
        var testUserDto = new UserDto { Id = _testUser.Id, Email = _testUser.Email, Password = _testUser.Password, };

        // Add test data to an in-memory database
        await _context.Set<UserAddress>().AddRangeAsync(_testAddresses);
        await _context.SaveChangesAsync();

        // Setup mocks
        _mockMapper = new Mock<IMapper>();
        _mockUserService = new Mock<IUserService>();

        // Setup UserService mock to return test user
        _mockUserService.Setup(x => x.GetUserByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(testUserDto);

        // Initialize the service with all dependencies
        _userAddressService = new UserAddressService(_context, _mockMapper.Object, _mockUserService.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    #region GroupByUserStateAsync Tests

    [Test]
    public async Task GroupByUserStateAsync_WithValidData_ReturnsGroupedByState()
    {
        // Act
        var result = await _userAddressService.GroupByUserStateAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2)); // NY and CA
        Assert.That(result.Any(g => g.Key == "NY"), Is.True);
        Assert.That(result.Any(g => g.Key == "CA"), Is.True);

        var nyGroup = result.First(g => g.Key == "NY");
        Assert.That(nyGroup.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task GroupByUserStateAsync_WithEmptyDatabase_ThrowsUserStateGroupException()
    {
        // Arrange - Clear the database
        _context.Set<UserAddress>().RemoveRange(_context.Set<UserAddress>());
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception =
            Assert.ThrowsAsync<UserStateGroupException>(async () => await _userAddressService.GroupByUserStateAsync());

        Assert.That(exception?.Message, Is.EqualTo("There are no states to group in the database"));
    }

    [Test]
    public async Task GroupByZipCodeAsync_WithValidData_ReturnsGroupedByZipCode()
    {
        // Act
        var grouped = await _context.UserAddress.GroupBy(u => u.ZipCode)
            .Select(g => new { ZipCode = g.Key, Count = g.Count() }).ToListAsync();

        // Assert
        Assert.That(grouped, Is.Not.Null);
        Assert.That(grouped.Count, Is.EqualTo(3)); // 10001, 90210, 10002
        Assert.That(grouped.Any(g => g.ZipCode == "10001"), Is.True);
        Assert.That(grouped.Any(g => g.ZipCode == "90210"), Is.True);
        Assert.That(grouped.Any(g => g.ZipCode == "10002"), Is.True);
    }

    [Test]
    public async Task GroupByZipCodeAsync_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Arrange - Clear the database
        _context.Set<UserAddress>().RemoveRange(_context.Set<UserAddress>());
        await _context.SaveChangesAsync();

        // Act
        var result = await _userAddressService.GroupByZipCodeAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(0));
    }

    #endregion

    #region GroupByCityAsync Tests

    [Test]
    public async Task GroupByCityAsync_WithValidData_ReturnsGroupedByCity()
    {
        // Act
        var result = await _userAddressService.GroupByCityAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2)); // New York and Los Angeles
        Assert.That(result.Any(g => g.Key == "New York"), Is.True);
        Assert.That(result.Any(g => g.Key == "Los Angeles"), Is.True);

        var nyGroup = result.First(g => g.Key == "New York");
        Assert.That(nyGroup.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task GroupByCityAsync_WithEmptyDatabase_ThrowsCityGroupingNotAvailableException()
    {
        // Arrange - Clear the database
        _context.Set<UserAddress>().RemoveRange(_context.Set<UserAddress>());
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception =
            Assert.ThrowsAsync<CityGroupingNotAvailableException>(async () =>
                await _userAddressService.GroupByCityAsync());

        Assert.That(exception?.Message, Is.EqualTo("There are no cities to group in the database"));
    }

    #endregion

    #region GetByUserAddressAsync Tests

    [Test]
    public async Task GetByUserAddressAsync_WithMatchingAddress1_ReturnsUserAddress()
    {
        // Arrange
        if (_testUser != null)
        {
            var searchAddress =
                new UserAddress(_testUser.Id, _testUser, "123 Main St", null, "Test City", "TS", "12345");

            // Act
            var result = await _userAddressService.GetByUserAddressAsync(searchAddress);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Address1, Is.EqualTo("123 Main St"));
        }
    }

    [Test]
    public async Task GetByUserAddressAsync_WithMatchingAddress2_ReturnsUserAddress()
    {
        // Arrange
        if (_testUser != null)
        {
            var searchAddress =
                new UserAddress(_testUser.Id, _testUser, "Different St", "Apt 2", "Test City", "TS", "12345");

            // Act
            var result = await _userAddressService.GetByUserAddressAsync(searchAddress);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Address2, Is.EqualTo("Apt 2"));
        }
    }

    [Test]
    public void GetByUserAddressAsync_WithNoMatch_ThrowsInvalidOperationException()
    {
        // Arrange
        if (_testUser == null)
        {
            return;
        }

        var searchAddress = new UserAddress(_testUser.Id, _testUser, "Non-existent St", "Non-existent Apt", "Test City",
            "TS", "12345");

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _userAddressService.GetByUserAddressAsync(searchAddress));
    }

    #endregion

    #region GetAllAsync Tests

    [Test]
    public async Task GetAllAsync_ReturnsAllAddresses()
    {
        // Act
        if (_testUser != null)
        {
            var result = await _userAddressService.GetAllAsync(_testUser.Id);

            // Assert
            var userAddresses = result.ToList();
            Assert.That(userAddresses, Is.Not.Null);
            Assert.That(userAddresses.Count(), Is.EqualTo(3));
        }
    }

    [Test]
    public async Task GetAllAsync_WithEmptyDatabase_ReturnsEmptyCollection()
    {
        // Arrange - Clear the database
        _context.Set<UserAddress>().RemoveRange(_context.Set<UserAddress>());
        await _context.SaveChangesAsync();

        // Act
        if (_testUser != null)
        {
            var result = await _userAddressService.GetAllAsync(_testUser.Id);

            // Assert
            var userAddresses = result.ToList();
            Assert.That(userAddresses, Is.Not.Null);
            Assert.That(userAddresses.Count(), Is.EqualTo(0));
        }
    }

    #endregion

    #region FindAsync Tests

    [Test]
    public async Task FindAsync_WithValidExpression_ReturnsMatchingAddresses()
    {
        // Act
        var result = await _userAddressService.FindAsync(ua => ua.State == "NY");

        // Assert
        var userAddresses = result.ToList();
        Assert.That(userAddresses, Is.Not.Null);
        Assert.That(userAddresses.Count(), Is.EqualTo(2));
        Assert.That(userAddresses.All(ua => ua.State == "NY"), Is.True);
    }

    [Test]
    public async Task FindAsync_WithNoMatches_ReturnsEmptyCollection()
    {
        // Act
        var result = await _userAddressService.FindAsync(ua => ua.State == "TX");

        // Assert
        var userAddresses = result.ToList();
        Assert.That(userAddresses, Is.Not.Null);
        Assert.That(userAddresses.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task FindAsync_WithComplexExpression_ReturnsMatchingAddresses()
    {
        // Act
        var result = await _userAddressService.FindAsync(ua => ua.City == "New York" && ua.ZipCode == "10002");


        // Assert
        var userAddresses = result.ToList();
        Assert.That(userAddresses, Is.Not.Null);
        Assert.That(userAddresses.Count, Is.EqualTo(1));
        Assert.That(userAddresses.First().ZipCode, Is.EqualTo("10002"));
    }

    #endregion

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_WithValidId_ReturnsUserAddress()
    {
        // Arrange
        var addressId = _testAddresses.First().Id;

        // Act
        var result = await _userAddressService.GetByIdAsync(addressId);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result?.Id, Is.EqualTo(addressId));
    }

    [Test]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var result = await _userAddressService.GetByIdAsync(invalidId);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetByIdAsync_WithEmptyGuid_ReturnsNull()
    {
        // Act
        var result = await _userAddressService.GetByIdAsync(Guid.Empty);

        // Assert
        Assert.That(result, Is.Null);
    }

    #endregion

    #region AddAsync Tests

    [Test]
    public async Task AddAsync_WithValidEntity_AddsEntityAndSavesChanges()
    {
        // Arrange
        if (_testUser != null)
        {
            var newAddress =
                new UserAddress(_testUser.Id, _testUser, "999 New St", null, "Chicago", "IL",
                    "60601") { Id = Guid.NewGuid() };

            // Act
            await _userAddressService.AddAsync(newAddress);

            // Assert
            var addedAddress = await _context.Set<UserAddress>().FindAsync(newAddress.Id);
            Assert.That(addedAddress, Is.Not.Null);
            Assert.That(addedAddress?.Address1, Is.EqualTo("999 New St"));
            Assert.That(addedAddress?.City, Is.EqualTo("Chicago"));
        }
    }

    [Test]
    public void AddAsync_WithNullEntity_ThrowsException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () => await _userAddressService.AddAsync(null));
    }

    #endregion

    #region AddRangeAsync Tests

    [Test]
    public async Task AddRangeAsync_WithValidEntities_AddsEntitiesAndSavesChanges()
    {
        // Arrange
        if (_testUser != null)
        {
            var newAddresses = new List<UserAddress>
            {
                new(_testUser.Id, _testUser, "111 First St", null, "Boston", "MA", "02101") { Id = Guid.NewGuid() },
                new(_testUser.Id, _testUser, "222 Second St", null, "Boston", "MA", "02102") { Id = Guid.NewGuid() }
            };

            // Act
            await _userAddressService.AddRangeAsync(newAddresses);
        }

        // Assert
        var allAddresses = await _context.Set<UserAddress>().ToListAsync();
        Assert.That(allAddresses.Count, Is.EqualTo(5)); // 3 original + 2 new

        var bostonAddresses = allAddresses.Where(ua => ua.City == "Boston").ToList();
        Assert.That(bostonAddresses.Count, Is.EqualTo(2));
    }

    [Test]
    public void AddRangeAsync_WithNullCollection_ThrowsException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () => await _userAddressService.AddRangeAsync(null!));
    }

    #endregion

    #region UpdateAsync Tests

    [Test]
    public async Task UpdateAsync_WithValidEntity_UpdatesEntityAndSavesChanges()
    {
        // Arrange
        var addressToUpdate = _testAddresses.First();
        var originalCity = addressToUpdate.City;
        addressToUpdate.City = "Updated City";

        // Act
        await _userAddressService.UpdateAsync(addressToUpdate);

        // Assert
        var updatedAddress = await _context.Set<UserAddress>().FindAsync(addressToUpdate.Id);
        Assert.That(updatedAddress, Is.Not.Null);
        Assert.That(updatedAddress?.City, Is.EqualTo("Updated City"));
        Assert.That(updatedAddress?.City, Is.Not.EqualTo(originalCity));
    }

    [Test]
    public void UpdateAsync_WithNullEntity_ThrowsException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () => await _userAddressService.UpdateAsync(null));
    }

    #endregion

    #region RemoveAsync Tests

    [Test]
    public async Task RemoveAsync_WithValidEntity_RemovesEntityAndSavesChanges()
    {
        // Arrange
        var addressToRemove = _testAddresses.First();
        var addressId = addressToRemove.Id;

        // Act
        await _userAddressService.RemoveAsync(addressToRemove);

        // Assert
        var removedAddress = await _context.Set<UserAddress>().FindAsync(addressId);
        Assert.That(removedAddress, Is.Null);

        var remainingAddresses = await _context.Set<UserAddress>().ToListAsync();
        Assert.That(remainingAddresses.Count, Is.EqualTo(2));
    }

    [Test]
    public void RemoveAsync_WithNullEntity_ThrowsException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () => await _userAddressService.RemoveAsync(null));
    }

    #endregion

    #region UpdateUserAddress Tests

    [Test]
    public async Task UpdateUserAddress_WithValidRequest_UpdatesAndReturnsAddress()
    {
        // Arrange
        var existingAddress = _testAddresses.First();
        if (_testUser != null)
        {
            var updateRequest = new UserAddressRequest
            {
                Id = existingAddress.Id, UserId = _testUser.Id, Address1 = "Updated Address", City = "Updated City"
            };

            _mockMapper.Setup(m => m.Map(It.IsAny<UserAddressRequest>(), It.IsAny<UserAddress>()))
                .Callback<UserAddressRequest, UserAddress>((src, dest) =>
                {
                    dest.Address1 = src.Address1 ?? string.Empty;
                    dest.City = src.City;
                });

            // Act
            var result = await _userAddressService.UpdateUserAddress(updateRequest);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo(existingAddress.Id));
            _mockMapper.Verify(m => m.Map(updateRequest, It.IsAny<UserAddress>()), Times.Once);
        }

        // Verify the address was actually updated in the database
        var updatedAddress = await _context.Set<UserAddress>().FindAsync(existingAddress.Id);
        Assert.That(updatedAddress?.Address1, Is.EqualTo("Updated Address"));
        Assert.That(updatedAddress?.City, Is.EqualTo("Updated City"));
    }

    [Test]
    public void UpdateUserAddress_WithNonExistentUser_ThrowsArgumentException()
    {
        // Arrange
        _mockUserService.Setup(x => x.GetUserByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((UserDto)null!);

        var updateRequest = new UserAddressRequest
        {
            Id = _testAddresses.First().Id, UserId = Guid.NewGuid(), Address1 = "Some Address"
        };

        // Act & Assert
        var exception =
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _userAddressService.UpdateUserAddress(updateRequest));

        Assert.That(exception?.Message, Is.EqualTo("User not found, cannot update address."));
    }

    [Test]
    public void UpdateUserAddress_WithNonExistentAddress_ThrowsArgumentNullException()
    {
        // Arrange
        if (_testUser == null)
        {
            return;
        }

        var updateRequest = new UserAddressRequest
        {
            Id = Guid.NewGuid(), UserId = _testUser.Id, Address1 = "Some Address"
        };

        // Act & Assert
        var exception =
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _userAddressService.UpdateUserAddress(updateRequest));

        Assert.That(exception?.ParamName, Is.EqualTo("existingAddress"));
        Assert.That(exception?.Message, Does.Contain("User Address does not exist"));
    }

    [Test]
    public void UpdateUserAddress_WithEmptyGuid_ThrowsArgumentNullException()
    {
        // Arrange
        if (_testUser == null)
        {
            return;
        }

        var updateRequest = new UserAddressRequest
        {
            Id = Guid.Empty, UserId = _testUser.Id, Address1 = "Some Address"
        };

        // Act & Assert
        var exception =
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await _userAddressService.UpdateUserAddress(updateRequest));

        Assert.That(exception?.ParamName, Is.EqualTo("existingAddress"));
    }

    #endregion

    #region Integration Tests

    [Test]
    public async Task CompleteWorkflow_AddFindUpdateRemove_WorksCorrectly()
    {
        // Arrange
        if (_testUser != null)
        {
            var newAddress =
                new UserAddress(_testUser.Id, _testUser, "Integration Test St", null, "Test City", "TC", "12345")
                {
                    Id = Guid.NewGuid()
                };

            // Add
            await _userAddressService.AddAsync(newAddress);
            var addedAddress = await _userAddressService.GetByIdAsync(newAddress.Id);
            Assert.That(addedAddress, Is.Not.Null);

            // Find
            var foundAddresses = await _userAddressService.FindAsync(ua => ua.City == "Test City");
            Assert.That(foundAddresses.Count(), Is.EqualTo(1));

            // Update
            if (addedAddress != null)
            {
                addedAddress.City = "Updated Test City";
                await _userAddressService.UpdateAsync(addedAddress);
            }

            var updatedAddress = await _userAddressService.GetByIdAsync(newAddress.Id);
            Assert.That(updatedAddress?.City, Is.EqualTo("Updated Test City"));

            // Remove
            if (updatedAddress != null)
            {
                await _userAddressService.RemoveAsync(updatedAddress);
            }

            var removedAddress = await _userAddressService.GetByIdAsync(newAddress.Id);
            Assert.That(removedAddress, Is.Null);
        }
    }

    #endregion
}
