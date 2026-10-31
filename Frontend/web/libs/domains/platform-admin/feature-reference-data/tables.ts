import {
  ReferenceColumn,
  ReferenceField,
  ReferenceFilter,
  ReferenceTable,
  referenceTitle,
} from '@harmony/platform-admin/domain';
import { ASCENDING, SortOrder } from '@harmony/shared/domain';

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

// A setting key: the owning area in lower case, then dotted words. Matches "frontoffice.checkInTime"; rejects
// "FrontOffice.x" and "frontoffice". Tester: https://regex101.com/?regex=%5E%5Cs*%5Ba-z%5D%5Ba-z0-9%5D*(%5C.%5Ba-zA-Z%5D%5Ba-zA-Z0-9-%5D*)%2B%5Cs*%24&testString=frontoffice.checkInTime
const settingKey = /^\s*[a-z][a-z0-9]*(\.[a-zA-Z][a-zA-Z0-9-]*)+\s*$/;
// A service name: a letter, then 1 to 63 letters or digits. Matches "FrontOffice"; rejects "Front Office" and "2Pms".
// Tester: https://regex101.com/?regex=%5E%5Cs*%5BA-Za-z%5D%5BA-Za-z0-9%5D%7B1%2C63%7D%5Cs*%24&testString=FrontOffice
const serviceName = /^\s*[A-Za-z][A-Za-z0-9]{1,63}\s*$/;
const KEY_MAX_LENGTH = 128;
const DEFAULT_MAX_LENGTH = 1000;

const ascending = (memberName: string): SortOrder => ({ memberName, sortOrder: ASCENDING });

/* ── Field makers: the wording the platform app had, label plus a hint with the standard and an example ── */
const nameEn = (hint: string): ReferenceField => ({
  key: 'nameEn',
  label: $localize`:@@platform-admin.reference.field.name-en:Name (English)`,
  kind: 'text',
  required: true,
  maxLength: NAME_MAX_LENGTH,
  lang: 'en',
  dir: 'ltr',
  hint,
});
const nameAr = (placeholder: string): ReferenceField => ({
  key: 'nameAr',
  label: $localize`:@@platform-admin.reference.field.name-ar:Name (Arabic)`,
  kind: 'text',
  required: true,
  requiredCode: '10408',
  maxLength: NAME_MAX_LENGTH,
  lang: 'ar',
  dir: 'rtl',
  placeholder,
});
const isActive: ReferenceField = {
  key: 'isActive',
  label: $localize`:@@platform-admin.reference.field.active:Active`,
  kind: 'checkbox',
  editOnly: true,
  hint: $localize`:@@platform-admin.reference.hint.active:Untick to retire the row: it stays, because other rows point at it, but is offered to nobody new.`,
};
const countryCode = (hint: string): ReferenceField => ({
  key: 'countryCode',
  label: $localize`:@@platform-admin.reference.field.country:Country`,
  kind: 'select',
  required: true,
  requiredCode: '10404',
  createOnly: true,
  optionsFrom: { table: 'Country', value: 'code', label: 'nameEn' },
  hint,
});
const code = (
  label: string,
  patternOf: RegExp,
  hint: string,
  placeholder: string,
  patternCode = '10402',
): ReferenceField => ({
  key: 'code',
  label,
  kind: 'text',
  required: true,
  isKey: true,
  pattern: patternOf,
  patternCode,
  dir: 'ltr',
  hint,
  placeholder,
});
const ownerService = (hint: string, placeholder: string): ReferenceField => ({
  key: 'ownerService',
  label: $localize`:@@platform-admin.reference.field.owner-service:Owning service`,
  kind: 'text',
  required: true,
  requiredCode: '10410',
  dir: 'ltr',
  hint,
  placeholder,
});
const descriptionField: ReferenceField = {
  key: 'description',
  label: $localize`:@@platform-admin.reference.field.description:Description`,
  kind: 'text',
  maxLength: DESCRIPTION_MAX_LENGTH,
  dir: 'auto' as 'ltr',
  optional: true,
  hint: $localize`:@@platform-admin.reference.hint.description:A sentence for the next administrator: what the rows are used for.`,
};
const lookupName = (hint: string, requiredCode: string): ReferenceField => ({
  key: 'name',
  label: $localize`:@@platform-admin.reference.field.name:Name`,
  kind: 'text',
  required: true,
  requiredCode,
  maxLength: LOOKUP_NAME_MAX_LENGTH,
  hint,
});
const isGlobal: ReferenceField = {
  key: 'isGlobal',
  label: $localize`:@@platform-admin.reference.field.global:Global: every tenant sees it`,
  kind: 'checkbox',
  createOnly: true,
  hint: $localize`:@@platform-admin.reference.hint.global:Chosen once: a global row belongs to the platform and shows in every hotel; a tenant row stays with its tenant.`,
};

/* ── Column labels, in words ───────────────────────────────────────── */
const col = {
  code: $localize`:@@platform-admin.reference.column.code:Code`,
  nameEn: $localize`:@@platform-admin.reference.column.name-en:English name`,
  nameAr: $localize`:@@platform-admin.reference.column.name-ar:Arabic name`,
  state: $localize`:@@platform-admin.reference.column.state:State`,
  name: $localize`:@@platform-admin.reference.column.name:Name`,
  country: $localize`:@@platform-admin.reference.column.country:Country`,
  kind: $localize`:@@platform-admin.reference.column.kind:Kind`,
  ownerService: $localize`:@@platform-admin.reference.column.owner-service:Owner service`,
};
const codeColumn: ReferenceColumn = { key: 'code', label: col.code, sort: 'Code' };
const nameEnColumn: ReferenceColumn = { key: 'nameEn', label: col.nameEn, sort: 'NameEn' };
const nameArColumn: ReferenceColumn = {
  key: 'nameAr',
  label: col.nameAr,
  kind: 'rtl',
  sort: 'NameAr',
};
const stateColumn: ReferenceColumn = {
  key: 'isActive',
  label: col.state,
  kind: 'onoff',
  sort: 'IsActive',
};

/* ── Filters ───────────────────────────────────────────────────────── */
const searchFilter: ReferenceFilter = {
  key: 'searchText',
  label: $localize`:@@platform-admin.reference.filter.search:Search`,
  kind: 'text',
  placeholder: $localize`:@@platform-admin.reference.filter.search-placeholder:code or name, 2+ characters`,
};
const stateFilter: ReferenceFilter = {
  key: 'isActive',
  label: col.state,
  kind: 'select',
  options: [
    { value: '', label: $localize`:@@platform-admin.reference.filter.all:All` },
    { value: 'true', label: $localize`:@@platform-admin.reference.cell.active:Active` },
    { value: 'false', label: $localize`:@@platform-admin.reference.cell.inactive:Inactive` },
  ],
};
const countryFilter: ReferenceFilter = {
  key: 'countryCode',
  label: col.country,
  kind: 'lookup',
  optionsFrom: { table: 'Country', value: 'code', label: 'nameEn' },
};

/* ── The tables ────────────────────────────────────────────────────── */
export const tables: readonly ReferenceTable[] = [
  {
    key: 'currencies',
    controller: 'Currency',
    title: referenceTitle('currencies'),
    singular: $localize`:@@platform-admin.reference.currencies.singular:currency`,
    description: $localize`:@@platform-admin.reference.currencies.description:The currencies a hotel can price, invoice and pay in. Minor units (2 or 3 decimals) come from the money registry, never typed, so money rounds the same everywhere.`,
    idField: 'code',
    fields: [
      code(
        $localize`:@@platform-admin.reference.field.currency-code:Currency code`,
        threeLetters,
        $localize`:@@platform-admin.reference.hint.currency-code:3 letters from the ISO 4217 list, like JOD, SAR or AED. Must be a currency Harmony can price in. Fixed once saved.`,
        'JOD',
      ),
      nameEn(
        $localize`:@@platform-admin.reference.hint.currency-name:As printed on invoices, like Jordanian dinar.`,
      ),
      nameAr('دينار أردني'),
      isActive,
    ],
    columns: [
      codeColumn,
      nameEnColumn,
      nameArColumn,
      {
        key: 'minorUnits',
        label: $localize`:@@platform-admin.reference.column.decimals:Decimals`,
        kind: 'number',
        sort: 'MinorUnits',
      },
      stateColumn,
    ],
    filters: [searchFilter, stateFilter],
    defaultSort: [ascending('Code')],
  },
  {
    key: 'time-zones',
    controller: 'TimeZone',
    title: referenceTitle('time-zones'),
    singular: $localize`:@@platform-admin.reference.time-zones.singular:time zone`,
    description: $localize`:@@platform-admin.reference.time-zones.description:The clocks hotels run on: business date, night audit, check-in time. A city points at one, so a hotel in Jordan can never be saved with a Riyadh clock.`,
    idField: 'id',
    fields: [
      {
        key: 'id',
        label: $localize`:@@platform-admin.reference.field.time-zone-id:Time zone`,
        kind: 'text',
        required: true,
        requiredCode: '10428',
        isKey: true,
        dir: 'ltr',
        placeholder: 'Asia/Amman',
        hint: $localize`:@@platform-admin.reference.hint.time-zone-id:The IANA zone id, Region/City, like Asia/Amman or Asia/Riyadh. One per real zone (Dubai and Muscat share Asia/Dubai). The server must recognise it. Fixed once saved.`,
      },
      nameEn(
        $localize`:@@platform-admin.reference.hint.time-zone-name:What people read in the dropdown, like Amman (Jordan).`,
      ),
      nameAr('عمّان'),
      isActive,
    ],
    columns: [
      { key: 'id', label: $localize`:@@platform-admin.reference.column.zone:Zone`, sort: 'Id' },
      nameEnColumn,
      nameArColumn,
      stateColumn,
    ],
    filters: [searchFilter, stateFilter],
    defaultSort: [ascending('Id')],
  },
  {
    key: 'languages',
    controller: 'Language',
    title: referenceTitle('languages'),
    singular: $localize`:@@platform-admin.reference.languages.singular:language`,
    description: $localize`:@@platform-admin.reference.languages.description:The languages guests and staff use: confirmation mails, registration cards and screens come out in them.`,
    idField: 'code',
    fields: [
      code(
        $localize`:@@platform-admin.reference.field.language-code:Language code`,
        twoLetters,
        $localize`:@@platform-admin.reference.hint.language-code:2 letters from the ISO 639-1 list, like ar, en or fr. Stored lower-case. Fixed once saved.`,
        'ar',
      ),
      nameEn(
        $localize`:@@platform-admin.reference.hint.language-name:The name an administrator reads, like Arabic.`,
      ),
      {
        key: 'nameNative',
        label: $localize`:@@platform-admin.reference.field.name-native:Native name`,
        kind: 'text',
        required: true,
        requiredCode: '10425',
        maxLength: NAME_MAX_LENGTH,
        placeholder: 'العربية',
        hint: $localize`:@@platform-admin.reference.hint.language-native:The name in the language itself, what a speaker reads when choosing it: العربية, Français, 日本語.`,
      },
      {
        key: 'isRightToLeft',
        label: $localize`:@@platform-admin.reference.field.rtl:Written right to left (Arabic, Hebrew, Urdu): screens flip their layout`,
        kind: 'checkbox',
      },
      isActive,
    ],
    columns: [
      codeColumn,
      nameEnColumn,
      {
        key: 'nameNative',
        label: $localize`:@@platform-admin.reference.column.native-name:Native name`,
        sort: 'NameNative',
      },
      {
        key: 'isRightToLeft',
        label: $localize`:@@platform-admin.reference.column.rtl:Right to left`,
        kind: 'bool',
        sort: 'IsRightToLeft',
      },
      stateColumn,
    ],
    filters: [searchFilter, stateFilter],
    defaultSort: [ascending('Code')],
  },
  {
    key: 'countries',
    controller: 'Country',
    title: referenceTitle('countries'),
    singular: $localize`:@@platform-admin.reference.countries.singular:country`,
    description: $localize`:@@platform-admin.reference.countries.description:Where hotels, companies and guests are from. From the country Harmony knows the currency to propose, the regions and cities to offer, the dial code, and later the tax and legal regimes.`,
    idField: 'code',
    fields: [
      code(
        $localize`:@@platform-admin.reference.field.country-code:Country code`,
        twoLetters,
        $localize`:@@platform-admin.reference.hint.country-code:2 letters from the ISO 3166-1 list, like JO, SA or AE. Addresses, nationalities and tax rules key on it. Fixed once saved.`,
        'JO',
      ),
      {
        key: 'alpha3',
        label: $localize`:@@platform-admin.reference.field.alpha3:Country code, 3 letters`,
        kind: 'text',
        required: true,
        createOnly: true,
        pattern: threeLetters,
        patternCode: '10413',
        dir: 'ltr',
        placeholder: 'JOR',
        hint: $localize`:@@platform-admin.reference.hint.country-alpha3:The same country in its 3-letter ISO form, like JOR, SAU or ARE: passports, bank files and e-invoicing use this one. Not the currency.`,
      },
      nameEn($localize`:@@platform-admin.reference.hint.country-name:Like Jordan or Saudi Arabia.`),
      nameAr('الأردن'),
      {
        key: 'dialCode',
        label: $localize`:@@platform-admin.reference.field.dial-code:Phone dial code`,
        kind: 'text',
        required: true,
        requiredCode: '10414',
        pattern: dialCode,
        patternCode: '10415',
        dir: 'ltr',
        placeholder: '+962',
        hint: $localize`:@@platform-admin.reference.hint.country-dial:A plus sign and the country digits, like +962 or +966. Phone numbers are stored against it.`,
      },
      {
        key: 'defaultCurrency',
        label: $localize`:@@platform-admin.reference.field.default-currency:Default currency`,
        kind: 'select',
        required: true,
        requiredCode: '10416',
        optionsFrom: { table: 'Currency', value: 'code', label: 'nameEn' },
        hint: $localize`:@@platform-admin.reference.hint.country-currency:What a hotel in this country prices in unless told otherwise. Add the currency first if it is not in the list.`,
      },
      isActive,
    ],
    columns: [
      codeColumn,
      {
        key: 'alpha3',
        label: $localize`:@@platform-admin.reference.column.alpha3:3-letter code`,
        sort: 'Alpha3',
      },
      nameEnColumn,
      nameArColumn,
      { key: 'dialCode', label: $localize`:@@platform-admin.reference.column.dial-code:Dial code` },
      {
        key: 'defaultCurrency',
        label: $localize`:@@platform-admin.reference.column.currency:Currency`,
        sort: 'DefaultCurrency',
      },
      stateColumn,
    ],
    filters: [searchFilter, stateFilter],
    defaultSort: [ascending('Code')],
  },
  {
    key: 'subdivisions',
    controller: 'CountrySubdivision',
    title: referenceTitle('subdivisions'),
    singular: $localize`:@@platform-admin.reference.subdivisions.singular:subdivision`,
    description: $localize`:@@platform-admin.reference.subdivisions.description:Governorates, regions, emirates, states: the level between a country and its cities. Addresses and reports group by them.`,
    idField: 'code',
    fields: [
      countryCode(
        $localize`:@@platform-admin.reference.hint.subdivision-country:The country this subdivision belongs to. Fixed once saved.`,
      ),
      code(
        $localize`:@@platform-admin.reference.field.subdivision-code:Subdivision code`,
        shortCode,
        $localize`:@@platform-admin.reference.hint.subdivision-code:From the ISO 3166-2 list: the country code, a hyphen, then 1 to 3 letters or digits, like JO-AM (Amman) or SA-01 (Riyadh). Fixed once saved.`,
        'JO-AM',
      ),
      nameEn($localize`:@@platform-admin.reference.hint.subdivision-name:Like Amman or Riyadh.`),
      nameAr('محافظة العاصمة'),
      {
        key: 'kind',
        label: $localize`:@@platform-admin.reference.field.kind:Kind`,
        kind: 'text',
        required: true,
        requiredCode: '10419',
        placeholder: 'Governorate',
        hint: $localize`:@@platform-admin.reference.hint.subdivision-kind:What this level is called in that country: Governorate, Region, Emirate, State, Province.`,
      },
    ],
    columns: [
      codeColumn,
      { key: 'countryCode', label: col.country, sort: 'CountryCode' },
      nameEnColumn,
      nameArColumn,
      { key: 'kind', label: col.kind, sort: 'Kind' },
    ],
    filters: [countryFilter, searchFilter],
    defaultSort: [ascending('Code')],
  },
  {
    key: 'cities',
    controller: 'City',
    title: referenceTitle('cities'),
    singular: $localize`:@@platform-admin.reference.cities.singular:city`,
    description: $localize`:@@platform-admin.reference.cities.description:Where a hotel stands. A city carries its country, subdivision and time zone, so choosing it fills all three for the hotel.`,
    idField: 'id',
    fields: [
      countryCode(
        $localize`:@@platform-admin.reference.hint.city-country:Fixed once saved: the city id is derived from the country and the English name.`,
      ),
      {
        key: 'subdivisionCode',
        label: $localize`:@@platform-admin.reference.field.subdivision:Subdivision`,
        kind: 'select',
        optional: true,
        optionsFrom: {
          table: 'CountrySubdivision',
          value: 'code',
          label: 'nameEn',
          narrowBy: 'countryCode',
        },
        hint: $localize`:@@platform-admin.reference.hint.city-subdivision:The governorate or region the city is in, from that country's list.`,
      },
      nameEn(
        $localize`:@@platform-admin.reference.hint.city-name:Like Amman or Riyadh. One name per country.`,
      ),
      nameAr('عمّان'),
      {
        key: 'timeZoneId',
        label: $localize`:@@platform-admin.reference.field.time-zone:Time zone`,
        kind: 'select',
        required: true,
        requiredCode: '10421',
        optionsFrom: { table: 'TimeZone', value: 'id', label: 'nameEn' },
        hint: $localize`:@@platform-admin.reference.hint.city-time-zone:The clock a hotel in this city runs on.`,
      },
      {
        key: 'latitude',
        label: $localize`:@@platform-admin.reference.field.latitude:Latitude`,
        kind: 'number',
        decimal: true,
        optional: true,
        dir: 'ltr',
        hint: $localize`:@@platform-admin.reference.hint.city-latitude:Decimal degrees, -90 to 90 (Amman 31.95). For maps and distances.`,
      },
      {
        key: 'longitude',
        label: $localize`:@@platform-admin.reference.field.longitude:Longitude`,
        kind: 'number',
        decimal: true,
        optional: true,
        dir: 'ltr',
        hint: $localize`:@@platform-admin.reference.hint.city-longitude:Decimal degrees, -180 to 180 (Amman 35.91).`,
      },
      isActive,
    ],
    columns: [
      nameEnColumn,
      nameArColumn,
      { key: 'countryCode', label: col.country, sort: 'CountryCode' },
      {
        key: 'subdivisionCode',
        label: $localize`:@@platform-admin.reference.column.subdivision:Subdivision`,
        sort: 'SubdivisionCode',
      },
      {
        key: 'timeZoneId',
        label: $localize`:@@platform-admin.reference.column.time-zone:Time zone`,
        sort: 'TimeZoneId',
      },
      stateColumn,
    ],
    filters: [countryFilter, searchFilter, stateFilter],
    defaultSort: [ascending('NameEn')],
  },
  {
    key: 'property-types',
    controller: 'PropertyType',
    title: referenceTitle('property-types'),
    singular: $localize`:@@platform-admin.reference.property-types.singular:property type`,
    description: $localize`:@@platform-admin.reference.property-types.description:What kind of place a property is: hotel, hostel, resort, serviced apartments. A row, so a new type needs no release.`,
    idField: 'code',
    fields: [
      code(
        $localize`:@@platform-admin.reference.field.type-code:Type code`,
        shortCode,
        $localize`:@@platform-admin.reference.hint.type-code:A short fixed code, like Hotel, Hostel or Resort: 2 to 32 letters, digits, dots, dashes or underscores. Fixed once saved.`,
        'Hotel',
      ),
      nameEn(
        $localize`:@@platform-admin.reference.hint.type-name:Like Hotel or Serviced apartments.`,
      ),
      nameAr('فندق'),
      {
        key: 'displayOrder',
        label: $localize`:@@platform-admin.reference.field.display-order:Display order`,
        kind: 'number',
        required: true,
        requiredCode: '10427',
        dir: 'ltr',
        hint: $localize`:@@platform-admin.reference.hint.display-order:Position in lists: 1 shows first. Zero or more.`,
      },
      isActive,
    ],
    columns: [
      {
        key: 'displayOrder',
        label: $localize`:@@platform-admin.reference.column.order:Order`,
        kind: 'number',
        sort: 'DisplayOrder',
      },
      codeColumn,
      nameEnColumn,
      nameArColumn,
      stateColumn,
    ],
    filters: [searchFilter, stateFilter],
    defaultSort: [ascending('DisplayOrder'), ascending('Code')],
  },
  {
    key: 'property-group-types',
    controller: 'PropertyGroupType',
    title: referenceTitle('property-group-types'),
    singular: $localize`:@@platform-admin.reference.property-group-types.singular:group type`,
    description: $localize`:@@platform-admin.reference.property-group-types.description:The ways hotels are grouped: by brand, by region, by owner. An exclusive type allows one group per property (one brand per hotel). A system row is one Harmony's own code relies on.`,
    idField: 'code',
    fields: [
      code(
        $localize`:@@platform-admin.reference.field.group-type-code:Group type code`,
        shortCode,
        $localize`:@@platform-admin.reference.hint.group-type-code:A short fixed code, like Brand, Region or Owner: 2 to 32 letters, digits, dots, dashes or underscores. Fixed once saved.`,
        'Brand',
      ),
      nameEn($localize`:@@platform-admin.reference.hint.group-type-name:Like Brand or Region.`),
      nameAr('علامة تجارية'),
      {
        key: 'isExclusive',
        label: $localize`:@@platform-admin.reference.field.exclusive:Exclusive: a property can belong to only one group of this type (one brand per hotel)`,
        kind: 'checkbox',
      },
      {
        key: 'isSystem',
        label: $localize`:@@platform-admin.reference.field.system:System row: Harmony's code relies on it; it can be renamed later but its exclusivity is locked`,
        kind: 'checkbox',
        createOnly: true,
      },
    ],
    columns: [
      codeColumn,
      nameEnColumn,
      nameArColumn,
      {
        key: 'isExclusive',
        label: $localize`:@@platform-admin.reference.column.exclusive:Exclusive`,
        kind: 'bool',
        sort: 'IsExclusive',
      },
      {
        key: 'isSystem',
        label: $localize`:@@platform-admin.reference.column.system:System`,
        kind: 'system',
        sort: 'IsSystem',
      },
    ],
    filters: [searchFilter],
    defaultSort: [ascending('Code')],
  },
  {
    key: 'modules',
    controller: 'Module',
    title: referenceTitle('modules'),
    singular: $localize`:@@platform-admin.reference.modules.singular:module`,
    description: $localize`:@@platform-admin.reference.modules.description:What a customer subscribes to: per hotel (reservations, POS) or once for the whole customer (loyalty, multi-property). Each service checks subscription lines against these codes, so a code never changes.`,
    idField: 'code',
    fields: [
      code(
        $localize`:@@platform-admin.reference.field.module-code:Module code`,
        shortCode,
        $localize`:@@platform-admin.reference.hint.module-code:What the owning service checks, like pms.core or multi.property: 2 to 32 letters, digits, dots, dashes or underscores. Fixed once saved.`,
        'pms.core',
      ),
      nameEn(
        $localize`:@@platform-admin.reference.hint.module-name:What a salesperson reads, like PMS core or Multiple properties.`,
      ),
      ownerService(
        $localize`:@@platform-admin.reference.hint.module-owner:The service that enforces it, like Identity, Fiscal or Guest.`,
        'Identity',
      ),
      {
        key: 'level',
        label: $localize`:@@platform-admin.reference.field.level:Sold per`,
        kind: 'select',
        required: true,
        requiredCode: '10429',
        options: [
          {
            value: 'Property',
            label: $localize`:@@platform-admin.reference.level.property:Property (one hotel; the subscription line names the hotel and its rooms)`,
          },
          {
            value: 'Tenant',
            label: $localize`:@@platform-admin.reference.level.tenant:Tenant (once for the whole customer, like loyalty or multi-property)`,
          },
        ],
        hint: $localize`:@@platform-admin.reference.hint.module-level:Hotel modules are sold per property; chain-wide modules to the company.`,
      },
      {
        key: 'hasLimit',
        label: $localize`:@@platform-admin.reference.field.has-limit:Has a limit: a subscription line carries a number (rooms, terminals)`,
        kind: 'checkbox',
      },
    ],
    columns: [
      codeColumn,
      { key: 'nameEn', label: col.name, sort: 'NameEn' },
      { key: 'ownerService', label: col.ownerService, sort: 'OwnerService' },
      {
        key: 'level',
        label: $localize`:@@platform-admin.reference.column.sold-per:Sold per`,
        sort: 'Level',
      },
      {
        key: 'hasLimit',
        label: $localize`:@@platform-admin.reference.column.has-limit:Has a limit`,
        kind: 'bool',
        sort: 'HasLimit',
      },
    ],
    filters: [
      {
        key: 'ownerService',
        label: col.ownerService,
        kind: 'text',
        placeholder: 'Identity',
      },
      {
        key: 'level',
        label: $localize`:@@platform-admin.reference.column.sold-per:Sold per`,
        kind: 'select',
        options: [
          { value: '', label: $localize`:@@platform-admin.reference.filter.all:All` },
          {
            value: 'Property',
            label: $localize`:@@platform-admin.reference.level.property.short:Property`,
          },
          {
            value: 'Tenant',
            label: $localize`:@@platform-admin.reference.level.tenant.short:Tenant`,
          },
        ],
      },
      searchFilter,
    ],
    defaultSort: [ascending('Code')],
  },
  {
    key: 'regulatory-environments',
    controller: 'RegulatoryEnvironment',
    title: referenceTitle('regulatory-environments'),
    singular: $localize`:@@platform-admin.reference.regulatory-environments.singular:regulatory environment`,
    description: $localize`:@@platform-admin.reference.regulatory-environments.description:The legal, tax, accounting and accommodation regimes a hotel lives under, one row per country and regime. A hotel in Jordan is under JO-GST; the Fiscal service knows what that means.`,
    idField: 'code',
    fields: [
      code(
        $localize`:@@platform-admin.reference.field.regime-code:Regime code`,
        shortCode,
        $localize`:@@platform-admin.reference.hint.regime-code:A short fixed code, usually country and regime, like JO-GST, SA-VAT or JO-LEGAL: 2 to 32 letters, digits, dots, dashes or underscores. Fixed once saved.`,
        'JO-GST',
      ),
      {
        key: 'kind',
        label: $localize`:@@platform-admin.reference.field.regime-kind:Kind of regime`,
        kind: 'select',
        required: true,
        requiredCode: '10403',
        createOnly: true,
        options: [
          {
            value: 'Legal',
            label: $localize`:@@platform-admin.reference.kind.legal:Legal (companies law, licensing)`,
          },
          {
            value: 'Tax',
            label: $localize`:@@platform-admin.reference.kind.tax:Tax (VAT, sales tax, tourism levy)`,
          },
          {
            value: 'Accounting',
            label: $localize`:@@platform-admin.reference.kind.accounting:Accounting (IFRS, local GAAP)`,
          },
          {
            value: 'Accommodation',
            label: $localize`:@@platform-admin.reference.kind.accommodation:Accommodation (tourism and hotel rules)`,
          },
        ],
        hint: $localize`:@@platform-admin.reference.hint.regime-kind:Fixed once saved.`,
      },
      countryCode(
        $localize`:@@platform-admin.reference.hint.regime-country:The country whose regime this is. Fixed once saved.`,
      ),
      nameEn(
        $localize`:@@platform-admin.reference.hint.regime-name:Like Jordan general sales tax or Saudi VAT.`,
      ),
      nameAr('ضريبة المبيعات العامة'),
      ownerService(
        $localize`:@@platform-admin.reference.hint.regime-owner:The service whose rules this regime stands for: Fiscal for tax and accounting, Identity for legal, Guest for accommodation.`,
        'Fiscal',
      ),
    ],
    columns: [
      codeColumn,
      { key: 'kind', label: col.kind, sort: 'Kind' },
      { key: 'countryCode', label: col.country, sort: 'CountryCode' },
      nameEnColumn,
      nameArColumn,
      { key: 'ownerService', label: col.ownerService },
    ],
    filters: [
      countryFilter,
      {
        key: 'kind',
        label: col.kind,
        kind: 'select',
        options: [
          { value: '', label: $localize`:@@platform-admin.reference.filter.all-kinds:All kinds` },
          { value: 'Legal', label: $localize`:@@platform-admin.reference.kind.legal.short:Legal` },
          { value: 'Tax', label: $localize`:@@platform-admin.reference.kind.tax.short:Tax` },
          {
            value: 'Accounting',
            label: $localize`:@@platform-admin.reference.kind.accounting.short:Accounting`,
          },
          {
            value: 'Accommodation',
            label: $localize`:@@platform-admin.reference.kind.accommodation.short:Accommodation`,
          },
        ],
      },
      searchFilter,
    ],
    defaultSort: [ascending('Code')],
  },
  {
    key: 'lookup-categories',
    controller: 'LookupCategory',
    title: referenceTitle('lookup-categories'),
    singular: $localize`:@@platform-admin.reference.lookup-categories.singular:lookup category`,
    description: $localize`:@@platform-admin.reference.lookup-categories.description:The named lists screens choose from (guest titles, room features, payment reasons): a category is the list, its values are the choices. Global ones show in every hotel.`,
    idField: 'id',
    fields: [
      lookupName(
        $localize`:@@platform-admin.reference.hint.category-name:What the list is called where a screen offers it, like Guest title or Room feature.`,
        '10102',
      ),
      descriptionField,
      isGlobal,
    ],
    columns: [
      { key: 'name', label: col.name, sort: 'Name' },
      {
        key: 'description',
        label: $localize`:@@platform-admin.reference.column.description:Description`,
      },
      {
        key: 'isGlobal',
        label: $localize`:@@platform-admin.reference.column.global:Global`,
        kind: 'bool',
        sort: 'IsGlobal',
      },
    ],
    filters: [searchFilter],
    defaultSort: [ascending('Name')],
    lookup: true,
    canDelete: true,
  },
  {
    key: 'lookup-values',
    controller: 'LookupValue',
    title: referenceTitle('lookup-values'),
    singular: $localize`:@@platform-admin.reference.lookup-values.singular:lookup value`,
    description: $localize`:@@platform-admin.reference.lookup-values.description:The choices inside one category, in the order and words a screen shows them. Choose the category first; retire a value rather than delete it once it has been used.`,
    idField: 'id',
    fields: [
      {
        key: 'lookupCategoryId',
        label: $localize`:@@platform-admin.reference.field.category:Category`,
        kind: 'select',
        required: true,
        requiredCode: '10106',
        createOnly: true,
        optionsFrom: { table: 'LookupCategory', value: 'id', label: 'name' },
        hint: $localize`:@@platform-admin.reference.hint.value-category:The list this value belongs to. Fixed once saved.`,
      },
      lookupName(
        $localize`:@@platform-admin.reference.hint.value-name:The words the screen shows, like Mr, Mrs or Sea view.`,
        '10107',
      ),
      descriptionField,
      {
        key: 'isActive',
        label: $localize`:@@platform-admin.reference.field.active:Active`,
        kind: 'checkbox',
        hint: $localize`:@@platform-admin.reference.hint.value-active:Untick to retire the value: what was chosen before keeps it; new choices do not see it.`,
      },
      isGlobal,
    ],
    columns: [
      { key: 'name', label: col.name, sort: 'Name' },
      {
        key: 'description',
        label: $localize`:@@platform-admin.reference.column.description:Description`,
      },
      stateColumn,
      {
        key: 'isGlobal',
        label: $localize`:@@platform-admin.reference.column.global:Global`,
        kind: 'bool',
        sort: 'IsGlobal',
      },
    ],
    filters: [searchFilter, stateFilter],
    defaultSort: [ascending('Name')],
    lookup: true,
    canDelete: true,
    scopedBy: 'lookupCategoryId',
  },
  {
    key: 'settings',
    controller: 'ConfigSetting',
    title: referenceTitle('settings'),
    singular: $localize`:@@platform-admin.reference.settings.singular:setting`,
    description: $localize`:@@platform-admin.reference.settings.description:The settings other services own, registered once: every customer can then set them for the whole chain, a group or one hotel. Identity keeps the values as text and decides which one wins at a hotel; the owning service decides what a value means.`,
    idField: 'key',
    fields: [
      {
        key: 'key',
        label: $localize`:@@platform-admin.reference.field.setting-key:Key`,
        kind: 'text',
        required: true,
        requiredCode: '10801',
        isKey: true,
        maxLength: KEY_MAX_LENGTH,
        pattern: settingKey,
        patternCode: '10802',
        dir: 'ltr',
        hint: $localize`:@@platform-admin.reference.hint.setting-key:The owning area in lower case, then dotted words, like frontoffice.checkInTime. Fixed once saved.`,
        placeholder: 'frontoffice.checkInTime',
      },
      {
        ...ownerService(
          $localize`:@@platform-admin.reference.hint.setting-owner:The service that reads the value and decides what it means, like FrontOffice.`,
          'FrontOffice',
        ),
        pattern: serviceName,
        patternCode: '10804',
      },
      {
        key: 'dataType',
        label: $localize`:@@platform-admin.reference.field.data-type:Data type`,
        kind: 'select',
        required: true,
        requiredCode: '10805',
        createOnly: true,
        options: [
          {
            value: 'time',
            label: $localize`:@@platform-admin.reference.data-type.time:Time (15:00)`,
          },
          {
            value: 'int',
            label: $localize`:@@platform-admin.reference.data-type.int:Whole number`,
          },
          {
            value: 'decimal',
            label: $localize`:@@platform-admin.reference.data-type.decimal:Number with decimals`,
          },
          { value: 'bool', label: $localize`:@@platform-admin.reference.data-type.bool:Yes or no` },
          { value: 'string', label: $localize`:@@platform-admin.reference.data-type.string:Text` },
        ],
        hint: $localize`:@@platform-admin.reference.hint.data-type:A hint for the screens and the owner; Identity never checks a value against it. Fixed once saved.`,
      },
      {
        key: 'systemDefault',
        label: $localize`:@@platform-admin.reference.field.system-default:Default`,
        kind: 'text',
        maxLength: DEFAULT_MAX_LENGTH,
        optional: true,
        dir: 'ltr',
        hint: $localize`:@@platform-admin.reference.hint.system-default:What wins when nobody set anything, like 15:00. Empty: no default.`,
      },
      {
        key: 'appliesToTypes',
        label: $localize`:@@platform-admin.reference.field.applies-to:For property types`,
        kind: 'text',
        list: true,
        optional: true,
        dir: 'ltr',
        hint: $localize`:@@platform-admin.reference.hint.applies-to:Property type codes separated by commas, like HOTEL, RESORT. Empty: every type.`,
      },
      isActive,
    ],
    columns: [
      {
        key: 'key',
        label: $localize`:@@platform-admin.reference.column.setting-key:Key`,
        sort: 'Key',
      },
      { key: 'ownerService', label: col.ownerService, sort: 'OwnerService' },
      {
        key: 'dataType',
        label: $localize`:@@platform-admin.reference.column.data-type:Data type`,
        sort: 'DataType',
      },
      {
        key: 'systemDefault',
        label: $localize`:@@platform-admin.reference.column.default:Default`,
      },
      {
        key: 'appliesToTypes',
        label: $localize`:@@platform-admin.reference.column.applies-to:For types`,
      },
      { key: 'isActive', label: col.state, kind: 'onoff' },
    ],
    filters: [
      {
        key: 'ownerService',
        label: col.ownerService,
        kind: 'text',
        placeholder: 'FrontOffice',
      },
      stateFilter,
      searchFilter,
    ],
    defaultSort: [ascending('Key')],
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
