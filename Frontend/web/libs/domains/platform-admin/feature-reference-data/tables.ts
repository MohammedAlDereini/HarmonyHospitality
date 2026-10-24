import { ReferenceField, ReferenceTable } from '@harmony/platform-admin/domain';

const NAME_MAX_LENGTH = 128;
const LOOKUP_NAME_MAX_LENGTH = 200;
const DESCRIPTION_MAX_LENGTH = 500;
// Two ASCII letters, with the surrounding spaces the form trims. Matches " jo "; rejects "jor".
// Tester: https://regex101.com/?regex=%5E%5Cs*%5BA-Za-z%5D%7B2%7D%5Cs*%24&testString=%20jo%20
const twoLetters = /^\s*[A-Za-z]{2}\s*$/;
// Three ASCII letters, with the surrounding spaces the form trims. Matches " jod "; rejects "jo".
// Tester: https://regex101.com/?regex=%5E%5Cs*%5BA-Za-z%5D%7B3%7D%5Cs*%24&testString=%20jod%20
const threeLetters = /^\s*[A-Za-z]{3}\s*$/;
// A dialling code: a plus sign and one to four digits. Matches "+962"; rejects "962" and "+96212345".
// Tester: https://regex101.com/?regex=%5E%5Cs*%5C%2B%5B0-9%5D%7B1%2C4%7D%5Cs*%24&testString=%2B962
const dialCode = /^\s*\+[0-9]{1,4}\s*$/;
// A short code: 2 to 32 letters, digits, dots, dashes or underscores. Matches "JO-Legal"; rejects "a b".
// Tester: https://regex101.com/?regex=%5E%5Cs*%5BA-Za-z0-9._-%5D%7B2%2C32%7D%5Cs*%24&testString=JO-Legal
const shortCode = /^\s*[A-Za-z0-9._-]{2,32}\s*$/;

const nameEn: ReferenceField = {
  key: 'nameEn',
  label: $localize`:@@platform-admin.reference.field.name-en:Name (English)`,
  kind: 'text',
  required: true,
  maxLength: NAME_MAX_LENGTH,
  lang: 'en',
  dir: 'ltr',
};
const nameAr: ReferenceField = {
  key: 'nameAr',
  label: $localize`:@@platform-admin.reference.field.name-ar:Name (Arabic)`,
  kind: 'text',
  required: true,
  maxLength: NAME_MAX_LENGTH,
  lang: 'ar',
  dir: 'rtl',
};
const isActive: ReferenceField = {
  key: 'isActive',
  label: $localize`:@@platform-admin.reference.field.active:Active`,
  kind: 'checkbox',
  editOnly: true,
};
const countryCode: ReferenceField = {
  key: 'countryCode',
  label: $localize`:@@platform-admin.reference.field.country:Country`,
  kind: 'select',
  required: true,
  createOnly: true,
  optionsFrom: { table: 'Country', value: 'code', label: 'nameEn' },
};
const code = (label: string, patternOf: RegExp, patternCode = '10402'): ReferenceField => ({
  key: 'code',
  label,
  kind: 'text',
  required: true,
  isKey: true,
  pattern: patternOf,
  patternCode,
  dir: 'ltr',
});
const codeLabel = $localize`:@@platform-admin.reference.field.code:Code`;
const descriptionField: ReferenceField = {
  key: 'description',
  label: $localize`:@@platform-admin.reference.field.description:Description`,
  kind: 'text',
  maxLength: DESCRIPTION_MAX_LENGTH,
  dir: 'auto' as 'ltr',
};
const lookupName: ReferenceField = {
  key: 'name',
  label: $localize`:@@platform-admin.reference.field.name:Name`,
  kind: 'text',
  required: true,
  maxLength: LOOKUP_NAME_MAX_LENGTH,
};
const isGlobal: ReferenceField = {
  key: 'isGlobal',
  label: $localize`:@@platform-admin.reference.field.global:Global (every tenant sees it)`,
  kind: 'checkbox',
  createOnly: true,
};

/** The reference tables the console edits, in the order the index shows them. */
export const tables: readonly ReferenceTable[] = [
  {
    key: 'currencies',
    controller: 'Currency',
    title: $localize`:@@platform-admin.reference.currencies:Currencies`,
    idField: 'code',
    fields: [code(codeLabel, threeLetters), nameEn, nameAr, isActive],
    columns: ['code', 'nameEn', 'nameAr', 'minorUnits', 'isActive'],
  },
  {
    key: 'countries',
    controller: 'Country',
    title: $localize`:@@platform-admin.reference.countries:Countries`,
    idField: 'code',
    fields: [
      code(codeLabel, twoLetters),
      {
        key: 'alpha3',
        label: $localize`:@@platform-admin.reference.field.alpha3:Three-letter code`,
        kind: 'text',
        required: true,
        createOnly: true,
        pattern: threeLetters,
        patternCode: '10413',
        dir: 'ltr',
      },
      nameEn,
      nameAr,
      {
        key: 'dialCode',
        label: $localize`:@@platform-admin.reference.field.dial-code:Dialling code`,
        kind: 'text',
        required: true,
        pattern: dialCode,
        patternCode: '10415',
        dir: 'ltr',
      },
      {
        key: 'defaultCurrency',
        label: $localize`:@@platform-admin.reference.field.default-currency:Default currency`,
        kind: 'select',
        required: true,
        optionsFrom: { table: 'Currency', value: 'code', label: 'nameEn' },
      },
      isActive,
    ],
    columns: ['code', 'alpha3', 'nameEn', 'nameAr', 'dialCode', 'defaultCurrency', 'isActive'],
  },
  {
    key: 'subdivisions',
    controller: 'CountrySubdivision',
    title: $localize`:@@platform-admin.reference.subdivisions:Country subdivisions`,
    idField: 'code',
    fields: [
      countryCode,
      code(codeLabel, shortCode),
      nameEn,
      nameAr,
      {
        key: 'kind',
        label: $localize`:@@platform-admin.reference.field.kind:Kind`,
        kind: 'text',
        required: true,
      },
    ],
    columns: ['countryCode', 'code', 'nameEn', 'nameAr', 'kind'],
  },
  {
    key: 'cities',
    controller: 'City',
    title: $localize`:@@platform-admin.reference.cities:Cities`,
    idField: 'id',
    fields: [
      countryCode,
      {
        key: 'subdivisionCode',
        label: $localize`:@@platform-admin.reference.field.subdivision:Subdivision code`,
        kind: 'text',
        dir: 'ltr',
      },
      nameEn,
      nameAr,
      {
        key: 'timeZoneId',
        label: $localize`:@@platform-admin.reference.field.time-zone:Time zone`,
        kind: 'select',
        required: true,
        optionsFrom: { table: 'TimeZone', value: 'id', label: 'nameEn' },
      },
      {
        key: 'latitude',
        label: $localize`:@@platform-admin.reference.field.latitude:Latitude`,
        kind: 'number',
        dir: 'ltr',
      },
      {
        key: 'longitude',
        label: $localize`:@@platform-admin.reference.field.longitude:Longitude`,
        kind: 'number',
        dir: 'ltr',
      },
      isActive,
    ],
    columns: ['nameEn', 'nameAr', 'countryCode', 'timeZoneId', 'isActive'],
  },
  {
    key: 'languages',
    controller: 'Language',
    title: $localize`:@@platform-admin.reference.languages:Languages`,
    idField: 'code',
    fields: [
      code(codeLabel, twoLetters),
      nameEn,
      {
        key: 'nameNative',
        label: $localize`:@@platform-admin.reference.field.name-native:Name in the language itself`,
        kind: 'text',
        required: true,
        maxLength: NAME_MAX_LENGTH,
      },
      {
        key: 'isRightToLeft',
        label: $localize`:@@platform-admin.reference.field.rtl:Written right to left`,
        kind: 'checkbox',
      },
      isActive,
    ],
    columns: ['code', 'nameEn', 'nameNative', 'isRightToLeft', 'isActive'],
  },
  {
    key: 'property-types',
    controller: 'PropertyType',
    title: $localize`:@@platform-admin.reference.property-types:Property types`,
    idField: 'code',
    fields: [
      code(codeLabel, shortCode),
      nameEn,
      nameAr,
      {
        key: 'displayOrder',
        label: $localize`:@@platform-admin.reference.field.display-order:Display order`,
        kind: 'number',
        required: true,
        dir: 'ltr',
      },
      isActive,
    ],
    columns: ['code', 'nameEn', 'nameAr', 'displayOrder', 'isActive'],
  },
  {
    key: 'property-group-types',
    controller: 'PropertyGroupType',
    title: $localize`:@@platform-admin.reference.property-group-types:Property group types`,
    idField: 'code',
    fields: [
      code(codeLabel, shortCode),
      nameEn,
      nameAr,
      {
        key: 'isExclusive',
        label: $localize`:@@platform-admin.reference.field.exclusive:Exclusive (a property joins one group of this type)`,
        kind: 'checkbox',
      },
      {
        key: 'isSystem',
        label: $localize`:@@platform-admin.reference.field.system:System (ships with the platform)`,
        kind: 'checkbox',
        createOnly: true,
      },
    ],
    columns: ['code', 'nameEn', 'nameAr', 'isExclusive', 'isSystem'],
  },
  {
    key: 'regulatory-environments',
    controller: 'RegulatoryEnvironment',
    title: $localize`:@@platform-admin.reference.regulatory-environments:Regulatory environments`,
    idField: 'code',
    fields: [
      code(codeLabel, shortCode),
      {
        key: 'kind',
        label: $localize`:@@platform-admin.reference.field.kind:Kind`,
        kind: 'select',
        required: true,
        createOnly: true,
        options: [
          { value: 'Legal', label: $localize`:@@platform-admin.reference.kind.legal:Legal` },
          { value: 'Tax', label: $localize`:@@platform-admin.reference.kind.tax:Tax` },
          {
            value: 'Accounting',
            label: $localize`:@@platform-admin.reference.kind.accounting:Accounting`,
          },
          {
            value: 'Accommodation',
            label: $localize`:@@platform-admin.reference.kind.accommodation:Accommodation`,
          },
        ],
      },
      countryCode,
      nameEn,
      nameAr,
      {
        key: 'ownerService',
        label: $localize`:@@platform-admin.reference.field.owner-service:Owning service`,
        kind: 'text',
        required: true,
        dir: 'ltr',
      },
    ],
    columns: ['code', 'kind', 'countryCode', 'nameEn', 'nameAr', 'ownerService'],
  },
  {
    key: 'time-zones',
    controller: 'TimeZone',
    title: $localize`:@@platform-admin.reference.time-zones:Time zones`,
    idField: 'id',
    fields: [
      {
        key: 'id',
        label: $localize`:@@platform-admin.reference.field.time-zone-id:IANA name`,
        kind: 'text',
        required: true,
        isKey: true,
        dir: 'ltr',
      },
      nameEn,
      nameAr,
      isActive,
    ],
    columns: ['id', 'nameEn', 'nameAr', 'isActive'],
  },
  {
    key: 'modules',
    controller: 'Module',
    title: $localize`:@@platform-admin.reference.modules:Modules`,
    idField: 'code',
    fields: [
      code(codeLabel, shortCode),
      nameEn,
      {
        key: 'ownerService',
        label: $localize`:@@platform-admin.reference.field.owner-service:Owning service`,
        kind: 'text',
        required: true,
        dir: 'ltr',
      },
      {
        key: 'level',
        label: $localize`:@@platform-admin.reference.field.level:Sold per`,
        kind: 'select',
        required: true,
        options: [
          {
            value: 'Property',
            label: $localize`:@@platform-admin.reference.level.property:Property`,
          },
          { value: 'Tenant', label: $localize`:@@platform-admin.reference.level.tenant:Tenant` },
        ],
      },
      {
        key: 'hasLimit',
        label: $localize`:@@platform-admin.reference.field.has-limit:Has a limit (rooms or terminals)`,
        kind: 'checkbox',
      },
    ],
    columns: ['code', 'nameEn', 'ownerService', 'level', 'hasLimit'],
  },
  {
    key: 'lookup-categories',
    controller: 'LookupCategory',
    title: $localize`:@@platform-admin.reference.lookup-categories:Lookup categories`,
    idField: 'id',
    fields: [lookupName, descriptionField, isGlobal],
    columns: ['name', 'description', 'isGlobal'],
    lookup: true,
    canDelete: true,
  },
  {
    key: 'lookup-values',
    controller: 'LookupValue',
    title: $localize`:@@platform-admin.reference.lookup-values:Lookup values`,
    idField: 'id',
    fields: [
      {
        key: 'lookupCategoryId',
        label: $localize`:@@platform-admin.reference.field.category:Category`,
        kind: 'select',
        required: true,
        createOnly: true,
        optionsFrom: { table: 'LookupCategory', value: 'id', label: 'name' },
      },
      lookupName,
      descriptionField,
      {
        key: 'isActive',
        label: $localize`:@@platform-admin.reference.field.active:Active`,
        kind: 'checkbox',
      },
      isGlobal,
    ],
    columns: ['name', 'description', 'isActive', 'isGlobal'],
    lookup: true,
    canDelete: true,
    scopedBy: 'lookupCategoryId',
  },
];

export const tableByKey = (key: string): ReferenceTable | null =>
  tables.find((table) => table.key === key) ?? null;

/** The field a server code belongs to, by the code's meaning; the key field takes the code codes. */
export const fieldForCode = (table: ReferenceTable, code: string): string | null => {
  const keyField = table.fields.find((field) => field.isKey)?.key ?? table.idField;
  const byCode: Readonly<Record<string, string>> = {
    '10401': keyField,
    '10402': keyField,
    '00056': keyField,
    '10406': 'nameEn',
    '10407': 'nameEn',
    '00057': 'nameEn',
    '10408': 'nameAr',
    '10409': 'nameAr',
    '10404': 'countryCode',
    '10405': 'countryCode',
    '00058': 'countryCode',
    '10410': 'ownerService',
    '10411': 'ownerService',
    '10412': 'alpha3',
    '10413': 'alpha3',
    '10414': 'dialCode',
    '10415': 'dialCode',
    '10403': 'kind',
    '00060': 'timeZoneId',
    '00061': 'defaultCurrency',
    '00062': 'code',
    '00001': 'name',
    '00003': 'name',
    '10102': 'name',
    '10103': 'name',
    '10107': 'name',
    '10108': 'name',
    '10104': 'description',
    '10109': 'description',
    '10106': 'lookupCategoryId',
  };
  const field = byCode[code];
  return field !== undefined && table.fields.some((f) => f.key === field) ? field : null;
};
