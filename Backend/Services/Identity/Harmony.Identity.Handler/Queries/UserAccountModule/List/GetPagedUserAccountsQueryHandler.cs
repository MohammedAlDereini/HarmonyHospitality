using System.Linq.Expressions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.List
{
    public class GetPagedUserAccountsQueryHandler : IRequestHandler<GetPagedUserAccountsQuery, CallResponse<PagedResult<UserAccountModel>>>
    {
        /// <summary>One projection for list and detail, so neither can ever select a hash or digest by accident.</summary>
        public static readonly Expression<Func<HarmonyUser, UserAccountModel>> Projection = a => new UserAccountModel
        {
            Id = a.Id,
            UserName = a.UserName!,
            Email = a.Email,
            DisplayName = a.DisplayName,
            IsServicePrincipal = a.IsServicePrincipal,
            TwoFactorEnabled = a.TwoFactorEnabled,
            State = a.State,
            StateReason = a.StateReason,
            SecurityVersion = a.SecurityVersion,
            ServiceCredentialIssuedOn = a.ServiceCredentialIssuedOn,
            CreatedDate = a.CreatedDate,
            CreatedBy = a.CreatedBy,
            ModifiedDate = a.ModifiedDate,
            ModifiedBy = a.ModifiedBy,
        };

        private readonly IUserAccountRepository userAccounts;

        public GetPagedUserAccountsQueryHandler(IUserAccountRepository userAccounts)
        {
            this.userAccounts = userAccounts;
        }

        public async Task<CallResponse<PagedResult<UserAccountModel>>> Handle(GetPagedUserAccountsQuery request, CancellationToken cancellationToken)
        {
            var keyword = request.SearchText?.Trim();
            var query = this.userAccounts.Query();

            query = query.WhereIf(request.IsServicePrincipal.HasValue, a => a.IsServicePrincipal == request.IsServicePrincipal!.Value);

            query = query.WhereIf(
                keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
                a => a.UserName!.Contains(keyword!) || a.DisplayName.Contains(keyword!) || (a.Email != null && a.Email.Contains(keyword!)));

            var pagedResult = await query.Select(Projection).GetPagedResultAsync(request, true, cancellationToken);

            return CallResponseBuilder.CreateResponse<PagedResult<UserAccountModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}
