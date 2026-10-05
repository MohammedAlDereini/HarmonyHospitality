/**
 * The ten reference tables, described once: which API they live behind, which field is the key, what a row shows,
 * what the form asks and how the list can be filtered. One page component renders them all from this.
 * Field names are the API's (camelCase as the JSON arrives). Labels say what the field means in plain words;
 * the hint under it names the standard and gives a real example.
 */
export type Row = Record<string, unknown>

export type FieldKind = 'text' | 'number' | 'bool' | 'select' | 'lookup'

export type Field = {
  name: string
  label: string
  kind: FieldKind
  /** Shown under the input. */
  hint?: string
  /** Extra attributes for text inputs. */
  placeholder?: string
  /** For a select: the fixed options. */
  options?: { value: string; label: string }[]
  /** For a lookup: which table the options come from, and which field of the form narrows them (a subdivision by its country). */
  lookup?: { table: string; narrowBy?: string }
  /** Part of the row's identity: asked on create, shown read-only on edit, never sent on update (the key is sent separately). */
  immutable?: boolean
  /** Only exists after the row does (IsActive). */
  editOnly?: boolean
  /** Only part of the create call (IsSystem on a group type). */
  createOnly?: boolean
  /** Arabic text: rendered right-to-left. */
  rtl?: boolean
  /** Not required: may be left empty and is then sent as null. */
  optional?: boolean
  /** Numbers: decimals allowed. */
  decimal?: boolean
}

export type Column = {
  name: string
  label: string
  kind?: 'text' | 'bool' | 'onoff' | 'system' | 'number' | 'rtl'
  /** The member name the API sorts by, when the column is sortable. */
  sort?: string
}

export type TableDef = {
  slug: string
  api: string
  title: string
  singular: string
  description: string
  /** The field that identifies a row in the model and in GET {key}. */
  key: string
  /** The name the Update call expects the key under. */
  updateKey: string
  columns: Column[]
  fields: Field[]
  filters: Field[]
  defaultSort: { memberName: string; sortOrder: 1 | 2 }[]
}

const nameEn: Field = { name: 'nameEn', label: 'English name', kind: 'text', hint: 'As people read it in lists and on documents.' }
const nameAr: Field = { name: 'nameAr', label: 'Arabic name', kind: 'text', rtl: true }
const isActive: Field = { name: 'isActive', label: 'Active', kind: 'bool', editOnly: true, hint: 'Untick to retire the row: it stays, because other rows point at it, but is offered to nobody new.' }
const searchText: Field = { name: 'searchText', label: 'Search', kind: 'text', placeholder: 'code or name, 2+ characters' }
const activeFilter: Field = { name: 'isActive', label: 'State', kind: 'select', options: [{ value: '', label: 'All' }, { value: 'true', label: 'Active' }, { value: 'false', label: 'Inactive' }] }
const countryFilter: Field = { name: 'countryCode', label: 'Country', kind: 'lookup', lookup: { table: 'countries' } }

export const tables: TableDef[] = [
  {
    slug: 'currencies', api: 'Currency', title: 'Currencies', singular: 'currency',
    description: 'The currencies a hotel can price, invoice and pay in. Minor units (2 or 3 decimals) come from the money registry, never typed, so money rounds the same everywhere.',
    key: 'code', updateKey: 'code',
    columns: [
      { name: 'code', label: 'Code', sort: 'Code' },
      { name: 'nameEn', label: 'English name', sort: 'NameEn' },
      { name: 'nameAr', label: 'Arabic name', kind: 'rtl', sort: 'NameAr' },
      { name: 'minorUnits', label: 'Decimals', kind: 'number', sort: 'MinorUnits' },
      { name: 'isActive', label: 'State', kind: 'onoff', sort: 'IsActive' },
    ],
    fields: [
      { name: 'code', label: 'Currency code', kind: 'text', immutable: true, placeholder: 'JOD', hint: '3 letters from the ISO 4217 list, like JOD, SAR or AED. Must be a currency Harmony can price in. Fixed once saved.' },
      { ...nameEn, hint: 'As printed on invoices, like Jordanian dinar.' },
      { ...nameAr, placeholder: 'دينار أردني' },
      isActive,
    ],
    filters: [searchText, activeFilter],
    defaultSort: [{ memberName: 'Code', sortOrder: 1 }],
  },
  {
    slug: 'time-zones', api: 'TimeZone', title: 'Time zones', singular: 'time zone',
    description: 'The clocks hotels run on: business date, night audit, check-in time. A city points at one, so a hotel in Jordan can never be saved with a Riyadh clock.',
    key: 'id', updateKey: 'id',
    columns: [
      { name: 'id', label: 'Zone', sort: 'Id' },
      { name: 'nameEn', label: 'English name', sort: 'NameEn' },
      { name: 'nameAr', label: 'Arabic name', kind: 'rtl', sort: 'NameAr' },
      { name: 'isActive', label: 'State', kind: 'onoff', sort: 'IsActive' },
    ],
    fields: [
      { name: 'id', label: 'Time zone', kind: 'text', immutable: true, placeholder: 'Asia/Amman', hint: 'The IANA zone id, Region/City, like Asia/Amman or Asia/Riyadh. One per real zone (Dubai and Muscat share Asia/Dubai). The server must recognise it.' },
      { ...nameEn, hint: 'What people read in the dropdown, like Amman (Jordan).' },
      { ...nameAr, placeholder: 'عمّان' },
      isActive,
    ],
    filters: [searchText, activeFilter],
    defaultSort: [{ memberName: 'Id', sortOrder: 1 }],
  },
  {
    slug: 'languages', api: 'Language', title: 'Languages', singular: 'language',
    description: 'The languages guests and staff use: confirmation mails, registration cards and screens come out in them.',
    key: 'code', updateKey: 'code',
    columns: [
      { name: 'code', label: 'Code', sort: 'Code' },
      { name: 'nameEn', label: 'English name', sort: 'NameEn' },
      { name: 'nameNative', label: 'Native name', sort: 'NameNative' },
      { name: 'isRightToLeft', label: 'Right to left', kind: 'bool', sort: 'IsRightToLeft' },
      { name: 'isActive', label: 'State', kind: 'onoff', sort: 'IsActive' },
    ],
    fields: [
      { name: 'code', label: 'Language code', kind: 'text', immutable: true, placeholder: 'ar', hint: '2 letters from the ISO 639-1 list, like ar, en or fr. Stored lower-case. Fixed once saved.' },
      { ...nameEn, hint: 'The name an administrator reads, like Arabic.' },
      { name: 'nameNative', label: 'Native name', kind: 'text', placeholder: 'العربية', hint: 'The name in the language itself, what a speaker reads when choosing it: العربية, Français, 日本語.' },
      { name: 'isRightToLeft', label: 'Written right to left (Arabic, Hebrew, Urdu): screens flip their layout', kind: 'bool' },
      isActive,
    ],
    filters: [searchText, activeFilter],
    defaultSort: [{ memberName: 'Code', sortOrder: 1 }],
  },
  {
    slug: 'countries', api: 'Country', title: 'Countries', singular: 'country',
    description: 'Where hotels, companies and guests are from. From the country Harmony knows the currency to propose, the regions and cities to offer, the dial code, and later the tax and legal regimes.',
    key: 'code', updateKey: 'code',
    columns: [
      { name: 'code', label: 'Code', sort: 'Code' },
      { name: 'alpha3', label: '3-letter code', sort: 'Alpha3' },
      { name: 'nameEn', label: 'English name', sort: 'NameEn' },
      { name: 'nameAr', label: 'Arabic name', kind: 'rtl', sort: 'NameAr' },
      { name: 'dialCode', label: 'Dial code' },
      { name: 'defaultCurrency', label: 'Currency', sort: 'DefaultCurrency' },
      { name: 'isActive', label: 'State', kind: 'onoff', sort: 'IsActive' },
    ],
    fields: [
      { name: 'code', label: 'Country code', kind: 'text', immutable: true, placeholder: 'JO', hint: '2 letters from the ISO 3166-1 list, like JO, SA or AE. Addresses, nationalities and tax rules key on it. Fixed once saved.' },
      { name: 'alpha3', label: 'Country code, 3 letters', kind: 'text', immutable: true, placeholder: 'JOR', hint: 'The same country in its 3-letter ISO form, like JOR, SAU or ARE: passports, bank files and e-invoicing use this one. Not the currency.' },
      { ...nameEn, hint: 'Like Jordan or Saudi Arabia.' },
      { ...nameAr, placeholder: 'الأردن' },
      { name: 'dialCode', label: 'Phone dial code', kind: 'text', placeholder: '+962', hint: 'A plus sign and the country digits, like +962 or +966. Phone numbers are stored against it.' },
      { name: 'defaultCurrency', label: 'Default currency', kind: 'lookup', lookup: { table: 'currencies' }, hint: 'What a hotel in this country prices in unless told otherwise. Add the currency first if it is not in the list.' },
      isActive,
    ],
    filters: [searchText, activeFilter],
    defaultSort: [{ memberName: 'Code', sortOrder: 1 }],
  },
  {
    slug: 'subdivisions', api: 'CountrySubdivision', title: 'Subdivisions', singular: 'subdivision',
    description: 'Governorates, regions, emirates, states: the level between a country and its cities. Addresses and reports group by them.',
    key: 'code', updateKey: 'code',
    columns: [
      { name: 'code', label: 'Code', sort: 'Code' },
      { name: 'countryCode', label: 'Country', sort: 'CountryCode' },
      { name: 'nameEn', label: 'English name', sort: 'NameEn' },
      { name: 'nameAr', label: 'Arabic name', kind: 'rtl', sort: 'NameAr' },
      { name: 'kind', label: 'Kind', sort: 'Kind' },
    ],
    fields: [
      { name: 'countryCode', label: 'Country', kind: 'lookup', lookup: { table: 'countries' }, immutable: true, hint: 'The country this subdivision belongs to. Fixed once saved.' },
      { name: 'code', label: 'Subdivision code', kind: 'text', immutable: true, placeholder: 'JO-AM', hint: 'From the ISO 3166-2 list: the country code, a hyphen, then 1 to 3 letters or digits, like JO-AM (Amman) or SA-01 (Riyadh). Fixed once saved.' },
      { ...nameEn, hint: 'Like Amman or Riyadh.' },
      { ...nameAr, placeholder: 'محافظة العاصمة' },
      { name: 'kind', label: 'Kind', kind: 'text', placeholder: 'Governorate', hint: 'What this level is called in that country: Governorate, Region, Emirate, State, Province.' },
    ],
    filters: [countryFilter, searchText],
    defaultSort: [{ memberName: 'Code', sortOrder: 1 }],
  },
  {
    slug: 'cities', api: 'City', title: 'Cities', singular: 'city',
    description: 'Where a hotel stands. A city carries its country, subdivision and time zone, so choosing it fills all three for the hotel.',
    key: 'id', updateKey: 'id',
    columns: [
      { name: 'nameEn', label: 'English name', sort: 'NameEn' },
      { name: 'nameAr', label: 'Arabic name', kind: 'rtl', sort: 'NameAr' },
      { name: 'countryCode', label: 'Country', sort: 'CountryCode' },
      { name: 'subdivisionCode', label: 'Subdivision', sort: 'SubdivisionCode' },
      { name: 'timeZoneId', label: 'Time zone', sort: 'TimeZoneId' },
      { name: 'latitude', label: 'Latitude', kind: 'number' },
      { name: 'longitude', label: 'Longitude', kind: 'number' },
      { name: 'isActive', label: 'State', kind: 'onoff', sort: 'IsActive' },
    ],
    fields: [
      { name: 'countryCode', label: 'Country', kind: 'lookup', lookup: { table: 'countries' }, immutable: true, hint: 'Fixed once saved: the city id is derived from country and English name.' },
      { name: 'subdivisionCode', label: 'Subdivision', kind: 'lookup', lookup: { table: 'subdivisions', narrowBy: 'countryCode' }, optional: true, hint: 'The governorate or region the city is in, from that country’s list.' },
      { ...nameEn, hint: 'Like Amman or Riyadh. One name per country.' },
      { ...nameAr, placeholder: 'عمّان' },
      { name: 'timeZoneId', label: 'Time zone', kind: 'lookup', lookup: { table: 'time-zones' }, hint: 'The clock a hotel in this city runs on.' },
      { name: 'latitude', label: 'Latitude', kind: 'number', decimal: true, optional: true, hint: 'Decimal degrees, -90 to 90 (Amman 31.95). For maps and distances.' },
      { name: 'longitude', label: 'Longitude', kind: 'number', decimal: true, optional: true, hint: 'Decimal degrees, -180 to 180 (Amman 35.91).' },
      isActive,
    ],
    filters: [countryFilter, { name: 'subdivisionCode', label: 'Subdivision', kind: 'lookup', lookup: { table: 'subdivisions', narrowBy: 'countryCode' } }, searchText, activeFilter],
    defaultSort: [{ memberName: 'NameEn', sortOrder: 1 }],
  },
  {
    slug: 'property-types', api: 'PropertyType', title: 'Property types', singular: 'property type',
    description: 'What kind of place a property is: hotel, hostel, resort, serviced apartments. A row, so a new type needs no release.',
    key: 'code', updateKey: 'code',
    columns: [
      { name: 'displayOrder', label: 'Order', kind: 'number', sort: 'DisplayOrder' },
      { name: 'code', label: 'Code', sort: 'Code' },
      { name: 'nameEn', label: 'English name', sort: 'NameEn' },
      { name: 'nameAr', label: 'Arabic name', kind: 'rtl', sort: 'NameAr' },
      { name: 'isActive', label: 'State', kind: 'onoff', sort: 'IsActive' },
    ],
    fields: [
      { name: 'code', label: 'Type code', kind: 'text', immutable: true, placeholder: 'Hotel', hint: 'A short fixed code, like Hotel, Hostel or Resort: 2 to 32 letters, digits, . - _  Fixed once saved.' },
      { ...nameEn, hint: 'Like Hotel or Serviced apartments.' },
      { ...nameAr, placeholder: 'فندق' },
      { name: 'displayOrder', label: 'Display order', kind: 'number', hint: 'Position in lists: 1 shows first. Zero or more.' },
      isActive,
    ],
    filters: [searchText, activeFilter],
    defaultSort: [{ memberName: 'DisplayOrder', sortOrder: 1 }, { memberName: 'Code', sortOrder: 1 }],
  },
  {
    slug: 'property-group-types', api: 'PropertyGroupType', title: 'Property group types', singular: 'group type',
    description: 'The ways hotels are grouped: by brand, by region, by owner. An exclusive type allows one group per property (one brand per hotel). A system row is one Harmony’s own code relies on.',
    key: 'code', updateKey: 'code',
    columns: [
      { name: 'code', label: 'Code', sort: 'Code' },
      { name: 'nameEn', label: 'English name', sort: 'NameEn' },
      { name: 'nameAr', label: 'Arabic name', kind: 'rtl', sort: 'NameAr' },
      { name: 'isExclusive', label: 'Exclusive', kind: 'bool', sort: 'IsExclusive' },
      { name: 'isSystem', label: 'System', kind: 'system', sort: 'IsSystem' },
    ],
    fields: [
      { name: 'code', label: 'Group type code', kind: 'text', immutable: true, placeholder: 'Brand', hint: 'A short fixed code, like Brand, Region or Owner: 2 to 32 letters, digits, . - _  Fixed once saved.' },
      { ...nameEn, hint: 'Like Brand or Region.' },
      { ...nameAr, placeholder: 'علامة تجارية' },
      { name: 'isExclusive', label: 'Exclusive: a property can belong to only one group of this type (one brand per hotel)', kind: 'bool' },
      { name: 'isSystem', label: 'System row: Harmony’s code relies on it; it can be renamed later but its exclusivity is locked', kind: 'bool', createOnly: true },
    ],
    filters: [searchText],
    defaultSort: [{ memberName: 'Code', sortOrder: 1 }],
  },
  {
    slug: 'capabilities', api: 'Capability', title: 'Capabilities', singular: 'capability',
    description: 'What a client can buy: the features a subscription turns on. Each service checks entitlements against these codes, so a code never changes.',
    key: 'code', updateKey: 'code',
    columns: [
      { name: 'code', label: 'Code', sort: 'Code' },
      { name: 'nameEn', label: 'Name', sort: 'NameEn' },
      { name: 'ownerService', label: 'Owner service', sort: 'OwnerService' },
      { name: 'hasLimit', label: 'Has a limit', kind: 'bool', sort: 'HasLimit' },
    ],
    fields: [
      { name: 'code', label: 'Capability code', kind: 'text', immutable: true, placeholder: 'pms.core', hint: 'What the owning service checks, like pms.core or multi.property: 2 to 64 letters, digits, . - _  Fixed once saved.' },
      { name: 'nameEn', label: 'Name', kind: 'text', hint: 'What a salesperson reads, like PMS core or Multiple properties.' },
      { name: 'ownerService', label: 'Owner service', kind: 'text', placeholder: 'Identity', hint: 'The service that enforces it, like Identity, Fiscal or Guest.' },
      { name: 'hasLimit', label: 'Has a limit: an entitlement carries a number (rooms, properties, users)', kind: 'bool' },
    ],
    filters: [{ name: 'ownerService', label: 'Owner service', kind: 'text', placeholder: 'Identity' }, searchText],
    defaultSort: [{ memberName: 'Code', sortOrder: 1 }],
  },
  {
    slug: 'regulatory-environments', api: 'RegulatoryEnvironment', title: 'Regulatory environments', singular: 'regulatory environment',
    description: 'The legal, tax, accounting and accommodation regimes a hotel lives under, one row per country and regime. A hotel in Jordan is under JO-GST; the Fiscal service knows what that means.',
    key: 'code', updateKey: 'code',
    columns: [
      { name: 'code', label: 'Code', sort: 'Code' },
      { name: 'kind', label: 'Kind', sort: 'Kind' },
      { name: 'countryCode', label: 'Country', sort: 'CountryCode' },
      { name: 'nameEn', label: 'English name', sort: 'NameEn' },
      { name: 'nameAr', label: 'Arabic name', kind: 'rtl', sort: 'NameAr' },
      { name: 'ownerService', label: 'Owner service' },
    ],
    fields: [
      { name: 'code', label: 'Regime code', kind: 'text', immutable: true, placeholder: 'JO-GST', hint: 'A short fixed code, usually country and regime, like JO-GST, SA-VAT or JO-LEGAL: 2 to 32 letters, digits, . - _  Fixed once saved.' },
      { name: 'kind', label: 'Kind of regime', kind: 'select', immutable: true, options: [{ value: 'Legal', label: 'Legal (companies law, licensing)' }, { value: 'Tax', label: 'Tax (VAT, sales tax, tourism levy)' }, { value: 'Accounting', label: 'Accounting (IFRS, local GAAP)' }, { value: 'Accommodation', label: 'Accommodation (tourism and hotel rules)' }], hint: 'Fixed once saved.' },
      { name: 'countryCode', label: 'Country', kind: 'lookup', lookup: { table: 'countries' }, immutable: true, hint: 'The country whose regime this is. Fixed once saved.' },
      { ...nameEn, hint: 'Like Jordan general sales tax or Saudi VAT.' },
      { ...nameAr, placeholder: 'ضريبة المبيعات العامة' },
      { name: 'ownerService', label: 'Owner service', kind: 'text', placeholder: 'Fiscal', hint: 'The service whose rules this regime stands for: Fiscal for tax and accounting, Identity for legal, Guest for accommodation.' },
    ],
    filters: [countryFilter, { name: 'kind', label: 'Kind', kind: 'select', options: [{ value: '', label: 'All kinds' }, { value: 'Legal', label: 'Legal' }, { value: 'Tax', label: 'Tax' }, { value: 'Accounting', label: 'Accounting' }, { value: 'Accommodation', label: 'Accommodation' }] }, searchText],
    defaultSort: [{ memberName: 'Code', sortOrder: 1 }],
  },
]

export const tableBySlug = (slug: string | undefined) => tables.find((t) => t.slug === slug)
