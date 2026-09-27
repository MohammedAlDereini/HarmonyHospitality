using Harmony.Identity.Domain.Models.RoleModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.RoleModule.List
{
    public class GetPagedRolesQueryHandler : IRequestHandler<GetPagedRolesQuery, CallResponse<PagedResult<RoleListModel>>>
    {
        private readonly IRoleRepository roleRepository;

        public GetPagedRolesQueryHandler(IRoleRepository roleRepository)
        {
            this.roleRepository = roleRepository;
        }

        public async Task<CallResponse<PagedResult<RoleListModel>>> Handle(GetPagedRolesQuery request, CancellationToken cancellationToken)
        {
            var keyword = request.SearchText?.Trim();

            var query = this.roleRepository.Query();

            query = query.WhereIf(
                keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
                r => r.Code.Contains(keyword!) || r.NameEn.Contains(keyword!) || (r.NameAr != null && r.NameAr.Contains(keyword!)));

            var projectedQuery = query.Select(r => new RoleListModel
            {
                Id = r.Id,
                Code = r.Code,
                NameEn = r.NameEn,
                NameAr = r.NameAr,
                PrivilegeLevel = r.PrivilegeLevel,
                IsSystemRole = r.IsSystemRole,
                IsSuperRole = r.IsSuperRole,
                CreatedDate = r.CreatedDate,
                CreatedBy = r.CreatedBy,
                ModifiedDate = r.ModifiedDate,
                ModifiedBy = r.ModifiedBy,
            });

            var pagedResult = await projectedQuery.GetPagedResultAsync(request, true, cancellationToken);

            return CallResponseBuilder.CreateResponse<PagedResult<RoleListModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}