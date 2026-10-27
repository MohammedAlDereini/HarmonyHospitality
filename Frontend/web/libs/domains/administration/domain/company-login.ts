/** Who proves a company person (Identity's AuthorityScheme, without the local one). */
export type CompanyProvider = 'Microsoft' | 'Google';

/** One row of GET /api/CompanyLogin/mine (CompanyLoginModel): a customer's company login and the domains it covers. */
export interface CompanyLogin {
  readonly id: string;
  readonly provider: CompanyProvider;
  /** Who signs the company's tokens; for Microsoft the company's tenant id is inside. */
  readonly issuer: string;
  readonly isEnabled: boolean;
  readonly domains: readonly string[];
  /** The link the company's IT opens once (Microsoft's admin consent); null for Google. */
  readonly consentUrl: string | null;
  readonly modifiedDate: string | null;
}

/** How a person signs in now (AccountSignInModel.SignsInWith). */
export type SignInMethod = 'Company' | 'Password';

/** A company identity bound to an account (CompanyLinkModel). */
export interface CompanyLink {
  readonly id: string;
  readonly provider: CompanyProvider;
  /** "first-sign-in", or the administrator's account id. */
  readonly linkedBy: string;
  readonly isOpen: boolean;
  readonly since: string;
  readonly until: string | null;
}

/** GET /api/CompanyLogin/account/{id} (AccountSignInModel): the administrator's choice, what covers the person, their links. */
export interface AccountSignIn {
  readonly passwordSignIn: boolean;
  readonly coveredBy: CompanyProvider | null;
  readonly signsInWith: SignInMethod;
  readonly links: readonly CompanyLink[];
}
