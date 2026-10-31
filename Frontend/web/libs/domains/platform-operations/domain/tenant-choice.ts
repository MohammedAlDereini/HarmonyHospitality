/** A customer as a filter names it (POST /api/Tenant/get-all, the few fields a select needs). */
export interface TenantChoice {
  readonly id: string;
  readonly code: string;
  readonly legalNameEn: string;
  readonly legalNameAr: string;
  readonly isPlatform: boolean;
}

/** The customer's name in the person's language. */
export const tenantChoiceName = (choice: TenantChoice, locale: string): string =>
  locale.startsWith('ar') && choice.legalNameAr !== '' ? choice.legalNameAr : choice.legalNameEn;
