/** The company logins Harmony offers (Identity's AuthorityScheme, without the local one). */
export type CompanyProvider = 'Microsoft' | 'Google';

/** The providers the console offers, in order. */
export const COMPANY_PROVIDERS: readonly CompanyProvider[] = ['Microsoft', 'Google'];

/** One row of GET /api/CompanyLogin/of/{tenantId} (CompanyLoginModel): a customer's company login. */
export interface TenantCompanyLogin {
  readonly id: string;
  readonly provider: CompanyProvider;
  /** Who signs the company's tokens; for Microsoft the company's tenant id is inside. */
  readonly issuer: string;
  readonly isEnabled: boolean;
  readonly domains: readonly string[];
  readonly consentUrl: string | null;
  readonly modifiedDate: string | null;
}
