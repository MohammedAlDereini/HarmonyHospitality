import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { PropertiesStore, ReferenceStore, TenantsStore } from '@harmony/platform-admin/data-access';
import { PropertyDraft, TenantDraft } from '@harmony/platform-admin/domain';
import { ApiResult } from '@harmony/platform/http';
import { PAGE_ACTIONS_IN_HEAD } from '@harmony/ui/shell';
import { NewTenantPage } from './new-tenant-page';

const CREATED = 201;
const TIER_FOUR = 4;
const ROOMS = 250;
const LAST_TWO = 2;
// Runs of whitespace, to fold the template's line breaks: matches '  ' (two spaces) and rejects 'a'. Tester: https://regex101.com/r/0bXzZc/1
const whitespace = /\s+/g;

const referenceRows: Readonly<Record<string, readonly Record<string, string>[]>> = {
  Country: [{ code: 'OM', nameEn: 'Oman', nameAr: 'عُمان' }],
  Currency: [{ code: 'OMR', nameEn: 'Omani rial', nameAr: 'ريال عماني' }],
  PropertyType: [{ code: 'HOTEL', nameEn: 'Hotel', nameAr: 'فندق' }],
  City: [{ id: 'c1', nameEn: 'Muscat', nameAr: 'مسقط' }],
  Language: [{ code: 'ar', nameEn: 'Arabic', nameAr: 'العربية' }],
  RegulatoryEnvironment: [
    { code: 'OM-LEGAL', nameEn: 'Oman legal', nameAr: '', kind: 'Legal' },
    { code: 'OM-TAX', nameEn: 'Oman tax', nameAr: '', kind: 'Tax' },
    { code: 'IFRS', nameEn: 'IFRS', nameAr: '', kind: 'Accounting' },
    { code: 'OM-HOTEL', nameEn: 'Oman hotel', nameAr: '', kind: 'Accommodation' },
  ],
  Module: [{ code: 'FO', nameEn: 'Front office', nameAr: '' }],
};

class FakeTenants {
  drafts: TenantDraft[] = [];
  create(draft: TenantDraft): Promise<ApiResult<unknown>> {
    this.drafts.push(draft);
    return Promise.resolve({ kind: 'ok', status: CREATED, data: 't-new' });
  }
}
class FakeProperties {
  drafts: PropertyDraft[] = [];
  create(draft: PropertyDraft): Promise<ApiResult<unknown>> {
    this.drafts.push(draft);
    return Promise.resolve({ kind: 'ok', status: CREATED, data: 'p-new' });
  }
}

describe('NewTenantPage', () => {
  let tenants: FakeTenants;
  let properties: FakeProperties;
  let fixture: ComponentFixture<NewTenantPage>;
  let page: HTMLElement;
  let navigated: string[];

  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 1));
    fixture.detectChanges();
    await fixture.whenStable();
  };
  const text = (selector: string): string =>
    page.querySelector(selector)?.textContent.replace(whitespace, ' ').trim() ?? '';
  const texts = (selector: string): string[] =>
    [...page.querySelectorAll(selector)].map((cell) =>
      cell.textContent.replace(whitespace, ' ').trim(),
    );
  const type = (selector: string, value: string): void => {
    const box = page.querySelector<HTMLInputElement | HTMLSelectElement>(selector);
    if (box === null) throw new Error(`no ${selector}`);
    box.value = value;
    box.dispatchEvent(new Event('input'));
    box.dispatchEvent(new Event('change'));
  };
  const press = async (act: string): Promise<void> => {
    page.querySelector<HTMLButtonElement>(`[data-act="${act}"]`)?.click();
    await settle();
  };
  const currentStep = (): string =>
    text('.c-step[data-state="current"] .step-btn > span:last-child');

  beforeEach(async () => {
    tenants = new FakeTenants();
    properties = new FakeProperties();
    navigated = [];
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PAGE_ACTIONS_IN_HEAD, useValue: true },
        { provide: TenantsStore, useValue: tenants },
        { provide: PropertiesStore, useValue: properties },
        {
          provide: ReferenceStore,
          useValue: {
            choices: (controller: string) => ({
              rows: signal(referenceRows[controller] ?? []),
              loading: signal(false),
              load: () => undefined,
            }),
          },
        },
      ],
    });
    const router = TestBed.inject(Router);
    router.navigateByUrl = (url: string | { toString(): string }): Promise<boolean> => {
      navigated.push(url.toString());
      return Promise.resolve(true);
    };
    fixture = TestBed.createComponent(NewTenantPage);
    page = fixture.nativeElement as HTMLElement;
    await settle();
  });

  it('starts on Company, refuses to move on with empty fields, then walks the steps and finishes with Tenant/Create only', async () => {
    expect(text('h1')).toBe('New tenant');
    expect(texts('.c-step .step-btn > span:last-child')).toEqual([
      'Company',
      'Administrator',
      'First hotel',
      'Modules',
      'Review and finish',
    ]);
    expect(currentStep()).toBe('Company');
    await press('next');
    expect(currentStep()).toBe('Company');
    expect(page.querySelectorAll('.field-error').length).toBeGreaterThan(0);
    type('#tenant-code', 'ABR');
    type('#tenant-name-en', 'Al Bustan Resorts');
    type('#tenant-name-ar', 'منتجعات البستان');
    type('#tenant-country', 'OM');
    type('#tenant-currency', 'OMR');
    type('#tenant-tier', String(TIER_FOUR));
    await settle();
    expect(text('h1')).toBe('New tenant · Al Bustan Resorts');
    await press('next');
    expect(currentStep()).toBe('Administrator');
    type('#tenant-admin-name', 'Fatma Al-Balushi');
    type('#tenant-admin-email', 'fatma@albustan.example');
    await settle();
    await press('next');
    expect(currentStep()).toBe('First hotel');
    await press('next');
    expect(currentStep()).toBe('Modules');
    expect(text('.step-card .note')).toContain('No hotel in this draft');
    await press('next');
    expect(currentStep()).toBe('Review and finish');
    expect(texts('[data-so-far] dd')).toEqual([
      'ABR · Al Bustan Resorts · منتجعات البستان',
      'Oman · OMR',
      '4 of 5',
      'Fatma Al-Balushi · fatma@albustan.example',
      'Sent on Finish · 24-hour link',
      'None',
      'None',
    ]);
    await press('finish');
    await settle();
    expect(tenants.drafts).toEqual([
      {
        code: 'ABR',
        legalNameEn: 'Al Bustan Resorts',
        legalNameAr: 'منتجعات البستان',
        countryCode: 'OM',
        defaultCurrency: 'OMR',
        tier: TIER_FOUR,
        externalIdentifier: '',
        administratorEmail: 'fatma@albustan.example',
        administratorDisplayName: 'Fatma Al-Balushi',
      },
    ]);
    expect(properties.drafts).toEqual([]);
    expect(navigated).toEqual(['/tenants/t-new']);
  });

  it('with a first hotel and a line, Finish sends Property/Create after the tenant, pointed at the new id', async () => {
    type('#tenant-code', 'ABR');
    type('#tenant-name-en', 'Al Bustan Resorts');
    type('#tenant-name-ar', 'منتجعات البستان');
    type('#tenant-country', 'OM');
    type('#tenant-currency', 'OMR');
    await settle();
    await press('next');
    type('#tenant-admin-name', 'Fatma Al-Balushi');
    type('#tenant-admin-email', 'fatma@albustan.example');
    await settle();
    await press('next');
    await press('want-hotel');
    type('#prop-code', 'MCT-BST');
    type('#prop-name-en', 'Al Bustan Palace');
    type('#prop-name-ar', 'قصر البستان');
    type('#prop-type', 'HOTEL');
    type('#prop-city', 'c1');
    type('#prop-address', 'Al Bustan Street');
    type('#prop-currency', 'OMR');
    type('#prop-language', 'ar');
    type('#prop-rooms', String(ROOMS));
    type('#prop-opens', '2027-03-01');
    type('#prop-legal', 'OM-LEGAL');
    type('#prop-tax', 'OM-TAX');
    type('#prop-accounting', 'IFRS');
    type('#prop-accommodation', 'OM-HOTEL');
    type('#prop-starts', '2027-03-01');
    await settle();
    await press('next');
    expect(currentStep()).toBe('Modules');
    type('#line-module', 'FO');
    type('#line-rooms', String(ROOMS));
    await settle();
    await press('add-line');
    expect(texts('.lines .nm')).toEqual(['FO · Front office']);
    await press('next');
    expect(texts('[data-so-far] dd').slice(-LAST_TWO)).toEqual([
      'MCT-BST · 250 rooms · opens 01 Mar 2027 · Muscat',
      'FO',
    ]);
    await press('finish');
    await settle();
    expect(tenants.drafts.length).toBe(1);
    expect(properties.drafts).toEqual([
      {
        tenantId: 't-new',
        code: 'MCT-BST',
        nameEn: 'Al Bustan Palace',
        nameAr: 'قصر البستان',
        propertyTypeCode: 'HOTEL',
        cityId: 'c1',
        addressLine: 'Al Bustan Street',
        postalCode: '',
        defaultCurrency: 'OMR',
        defaultLanguageCode: 'ar',
        totalRooms: ROOMS,
        legalEnvironmentCode: 'OM-LEGAL',
        taxEnvironmentCode: 'OM-TAX',
        accountingEnvironmentCode: 'IFRS',
        accommodationEnvironmentCode: 'OM-HOTEL',
        externalIdentifier: '',
        startsOn: '2027-03-01',
        lines: [{ moduleCode: 'FO', rooms: ROOMS, terminals: null, renewsAutomatically: true }],
      },
    ]);
    expect(navigated).toEqual(['/tenants/t-new']);
  });
});
