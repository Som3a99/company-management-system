using ERP.BLL.Common;
using ERP.BLL.Interfaces;
using ERP.DAL.Models;
using ERP.PL.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Tests.Infrastructure;

namespace Tests.Services
{
    public class RoleManagementServiceTests
    {
        [Fact]
        public async Task RemoveManagementRolesAsync_ShouldEvictClaimsAndRefreshSignIn()
        {
            using var db = TestDbContextFactory.Create();
            var user = new ApplicationUser { Id = "user-1", Email = "u@test.local" };

            var userManager = CreateMockUserManager();
            userManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
            userManager.Setup(x => x.IsInRoleAsync(user, "DepartmentManager")).ReturnsAsync(true);
            userManager.Setup(x => x.IsInRoleAsync(user, "ProjectManager")).ReturnsAsync(true);

            var signInManager = CreateMockSignInManager(userManager.Object);
            var cache = new Mock<ICacheService>();

            var sut = new RoleManagementService(
                userManager.Object,
                db,
                Mock.Of<ILogger<RoleManagementService>>(),
                signInManager.Object,
                cache.Object);

            await sut.RemoveManagementRolesAsync("user-1");

            userManager.Verify(x => x.RemoveFromRoleAsync(user, "DepartmentManager"), Times.Once);
            userManager.Verify(x => x.RemoveFromRoleAsync(user, "ProjectManager"), Times.Once);
            cache.Verify(x => x.RemoveAsync(CacheKeys.AvailableProjectManagersAll), Times.Once);
            cache.Verify(x => x.RemoveAsync($"{CacheKeys.UserClaimsPrefix}{user.Id}:claims"), Times.Once);
            signInManager.Verify(x => x.RefreshSignInAsync(user), Times.Once);
        }

        [Fact]
        public async Task RemoveManagementRolesAsync_ShouldNoOp_WhenUserIsMissing()
        {
            using var db = TestDbContextFactory.Create();
            var userManager = CreateMockUserManager();
            userManager.Setup(x => x.FindByIdAsync("unknown")).ReturnsAsync((ApplicationUser?)null);

            var sut = new RoleManagementService(
                userManager.Object,
                db,
                Mock.Of<ILogger<RoleManagementService>>(),
                CreateMockSignInManager(userManager.Object).Object,
                Mock.Of<ICacheService>());

            await sut.RemoveManagementRolesAsync("unknown");

            userManager.Verify(x => x.RemoveFromRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        }

        private static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(
                store.Object,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!);
        }

        private static Mock<SignInManager<ApplicationUser>> CreateMockSignInManager(UserManager<ApplicationUser> userManager)
        {
            var contextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            return new Mock<SignInManager<ApplicationUser>>(
                userManager,
                contextAccessor,
                Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
                null!,
                null!,
                null!,
                null!);
        }
    }
}
