using TBD.AddressModule.Models;
using TBD.API.DTOs.Users;

namespace TBD.AddressModule.Services;

public interface IUserAddressService
{
    Task<IEnumerable<UserAddress>> GetAllAsync(Guid userId);
    Task<UserAddress?> GetByIdAsync(Guid id);
    Task AddAsync(UserAddress entity);
    Task UpdateAsync(UserAddress entity);
    Task RemoveAsync(UserAddress entity);
    Task<UserAddress> UpdateUserAddress(UserAddressRequest userAddressDto);
}
