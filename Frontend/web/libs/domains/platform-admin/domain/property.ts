import { SubscriptionLine } from './tenant';

/** One contact of a property (PropertyContactModel). */
export interface PropertyContact {
  readonly id: string;
  readonly name: string;
  readonly role: string | null;
  readonly email: string | null;
  readonly telephone: string | null;
  readonly isPrimary: boolean;
}

/** One row of POST /api/Property/get-all and GET /api/Property/{id} (PropertyModel); only what screens read. */
export interface Property {
  readonly id: string;
  readonly tenantId: string;
  readonly code: string;
  readonly externalIdentifier: string | null;
  readonly nameEn: string;
  readonly nameAr: string;
  readonly propertyTypeCode: string;
  readonly cityId: string;
  readonly countryCode: string;
  readonly timeZoneId: string;
  readonly addressLine: string;
  readonly postalCode: string | null;
  readonly defaultCurrency: string;
  readonly defaultLanguageCode: string;
  readonly totalRooms: number;
  readonly legalEnvironmentCode: string;
  readonly taxEnvironmentCode: string;
  readonly accountingEnvironmentCode: string;
  readonly accommodationEnvironmentCode: string;
  readonly opensOn: string | null;
  readonly closesOn: string | null;
  readonly isActive: boolean;
  readonly licensedRooms: number | null;
  readonly roomsAboveLicence: boolean;
  /** The customer's code and the modules with a line, on the platform-wide list (C4). */
  readonly tenantCode: string | null;
  readonly moduleCodes: readonly string[];
  readonly subscriptions: readonly SubscriptionLine[];
  readonly contacts: readonly PropertyContact[];
}

/** The property's name in the person's language. */
export const propertyTitle = (
  property: Pick<Property, 'nameEn' | 'nameAr'>,
  locale: string,
): string =>
  locale.startsWith('ar') && property.nameAr !== '' ? property.nameAr : property.nameEn;

/** The first module a new property starts with (SubscriptionLineRequest). */
export interface PropertyLine {
  readonly moduleCode: string;
  readonly rooms: number | null;
  readonly terminals: number | null;
  readonly renewsAutomatically: boolean;
}

/** What the property creator writes (CreatePropertyCommand). */
export interface PropertyDraft {
  readonly tenantId: string;
  readonly code: string;
  readonly nameEn: string;
  readonly nameAr: string;
  readonly propertyTypeCode: string;
  readonly cityId: string;
  readonly addressLine: string;
  readonly postalCode: string;
  readonly defaultCurrency: string;
  readonly defaultLanguageCode: string;
  readonly totalRooms: number;
  readonly legalEnvironmentCode: string;
  readonly taxEnvironmentCode: string;
  readonly accountingEnvironmentCode: string;
  readonly accommodationEnvironmentCode: string;
  readonly externalIdentifier: string;
  readonly startsOn: string;
  readonly lines: readonly PropertyLine[];
}

/** What the property editor changes afterwards (UpdatePropertyCommand, the part the console shows). */
export interface PropertyUpdate {
  readonly nameEn: string;
  readonly nameAr: string;
  readonly addressLine: string;
  readonly postalCode: string;
  readonly totalRooms: number;
  readonly opensOn: string | null;
  readonly closesOn: string | null;
}

/** What the property list is filtered by; page is 1-based. */
/** A hotel's state as the list filters it: open today, opening later, or closed. */
export type PropertyState = 'Active' | 'Opening' | 'Closed';

export interface PropertyFilters {
  readonly tenantId: string | null;
  readonly search: string;
  readonly countryCode: string | null;
  readonly state: PropertyState | null;
  readonly typeCode: string | null;
  /** Only hotels declaring more rooms than they license today. */
  readonly aboveLicence: boolean;
  readonly page: number;
}
