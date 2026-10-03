using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Entities.Aggregates.PermissionModule;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Models.RoleModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.RoleModule.Detail
{
    public class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, CallResponse<RoleDetailModel>>
    {
        private readonly IRoleRepository roleRepository;
        private readonly IPermissionRepository permissionRepository;

        public GetRoleByIdQueryHandler(IRoleRepository roleRepository, IPermissionRepository permissionRepository)
        {
            this.roleRepository = roleRepository;
            this.permissionRepository = permissionRepository;
        }

        public async Task<CallResponse<RoleDetailModel>> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
        {
            var role = await this.roleRepository.Query()
                .Include(r => r.Permissions).ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            if (role is null)
            {
                return CallResponseBuilder.CreateResponse<RoleDetailModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(IdentityErrorCodes.RoleNotFound));
            }

            // A super role holds no rows: it has every active permission of the tenant.
            var permissions = role.IsSuperRole
                ? await this.permissionRepository.Query().Where(p => p.IsActive).ToListAsync(cancellationToken)
                : role.Permissions.Select(rp => rp.Permission).ToList();

            var model = new RoleDetailModel
            {
                Id = role.Id,
                Code = role.Code,
                NameEn = role.NameEn,
                NameAr = role.NameAr,
                PrivilegeLevel = role.PrivilegeLevel,
                IsSystemRole = role.IsSystemRole,
                IsSuperRole = role.IsSuperRole,
                IsPrivileged = role.IsPrivileged,
                Permissions = permissions.OrderBy(p => p.PermissionValue).Select(ToModel).ToList(),
                CreatedDate = role.CreatedDate,
                CreatedBy = role.CreatedBy,
                ModifiedDate = role.ModifiedDate,
                ModifiedBy = role.ModifiedBy,
            };

            return CallResponseBuilder.CreateResponse<RoleDetailModel>(eCallResponseStatus.Success).HasData(model);
        }

        private static PermissionModel ToModel(Permission permission) => new()
        {
            Id = permission.Id,
            PermissionValue = permission.PermissionValue,
            Name = permission.Name,
            Description = permission.Description,
            EndPoint = permission.EndPoint,
            IsActive = permission.IsActive,
        };
    }
}
