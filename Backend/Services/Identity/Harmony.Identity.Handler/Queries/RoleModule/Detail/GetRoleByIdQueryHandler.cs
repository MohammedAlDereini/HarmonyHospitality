using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Models.RoleModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.RoleModule.Detail
{
    public class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, CallResponse<RoleDetailModel>>
    {
        private readonly IRoleRepository roleRepository;

        public GetRoleByIdQueryHandler(IRoleRepository roleRepository)
        {
            this.roleRepository = roleRepository;
        }

        public async Task<CallResponse<RoleDetailModel>> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
        {
            var role = await this.roleRepository.Query().FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            if (role is null)
            {
                return CallResponseBuilder.CreateResponse<RoleDetailModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(IdentityErrorCodes.RoleNotFound));
            }

            // Mapped in memory: a super role's permissions are the whole catalogue, not a stored list.
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
                PermissionCodes = role.PermissionCodes.Order().ToList(),
                CreatedDate = role.CreatedDate,
                CreatedBy = role.CreatedBy,
                ModifiedDate = role.ModifiedDate,
                ModifiedBy = role.ModifiedBy,
            };

            return CallResponseBuilder.CreateResponse<RoleDetailModel>(eCallResponseStatus.Success).HasData(model);
        }
    }
}