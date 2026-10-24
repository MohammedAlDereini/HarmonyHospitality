/** One reference table as the places panel and the overview name it: the URL segment, Identity's controller, the title. */
export interface ReferenceCatalogueEntry {
  readonly key: string;
  readonly controller: string;
  readonly title: string;
}

/** The twelve reference tables, in the order the index and the places panel show them. */
export const referenceCatalogue: readonly ReferenceCatalogueEntry[] = [
  {
    key: 'currencies',
    controller: 'Currency',
    title: $localize`:@@platform-admin.reference.currencies:Currencies`,
  },
  {
    key: 'time-zones',
    controller: 'TimeZone',
    title: $localize`:@@platform-admin.reference.time-zones:Time zones`,
  },
  {
    key: 'languages',
    controller: 'Language',
    title: $localize`:@@platform-admin.reference.languages:Languages`,
  },
  {
    key: 'countries',
    controller: 'Country',
    title: $localize`:@@platform-admin.reference.countries:Countries`,
  },
  {
    key: 'subdivisions',
    controller: 'CountrySubdivision',
    title: $localize`:@@platform-admin.reference.subdivisions:Subdivisions`,
  },
  {
    key: 'cities',
    controller: 'City',
    title: $localize`:@@platform-admin.reference.cities:Cities`,
  },
  {
    key: 'property-types',
    controller: 'PropertyType',
    title: $localize`:@@platform-admin.reference.property-types:Property types`,
  },
  {
    key: 'property-group-types',
    controller: 'PropertyGroupType',
    title: $localize`:@@platform-admin.reference.property-group-types:Property group types`,
  },
  {
    key: 'modules',
    controller: 'Module',
    title: $localize`:@@platform-admin.reference.modules:Modules`,
  },
  {
    key: 'regulatory-environments',
    controller: 'RegulatoryEnvironment',
    title: $localize`:@@platform-admin.reference.regulatory-environments:Regulatory environments`,
  },
  {
    key: 'lookup-categories',
    controller: 'LookupCategory',
    title: $localize`:@@platform-admin.reference.lookup-categories:Lookup categories`,
  },
  {
    key: 'lookup-values',
    controller: 'LookupValue',
    title: $localize`:@@platform-admin.reference.lookup-values:Lookup values`,
  },
];

/** The title of one table by its URL segment; the segment itself when unknown (never silently blank). */
export const referenceTitle = (key: string): string =>
  referenceCatalogue.find((entry) => entry.key === key)?.title ?? key;
