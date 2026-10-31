import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { PAGE_ACTIONS_IN_HEAD, PageHead } from './page-head';
import { PageActionsTemplate } from './page-actions';

/** A detail page that offers one action and names itself. */
@Component({
  selector: 'hr-detail-stub',
  imports: [PageHead, PageActionsTemplate],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ng-template hrPageActions><button type="button" class="pbtn">Edit</button></ng-template>
    <hr-page-head title="Levant Hotels" lead="Jordan · JOD">
      <span slot="aside" class="aside">Tabs</span>
      <span slot="actions" class="extra">Invite</span>
    </hr-page-head>
  `,
})
class DetailStub {}

@Component({ selector: 'hr-list-stub', template: '' })
class ListStub {}

/** A page named after its section, with a tile before the title. */
@Component({
  selector: 'hr-section-stub',
  imports: [PageHead],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hr-page-head title="Tenants">
      <span slot="before-title" class="tile">T</span>
      <span slot="after-title" class="chip">Active</span>
    </hr-page-head>
  `,
})
class SectionStub {}

const routes = [
  {
    path: 'tenants',
    data: { crumb: 'Tenants' },
    children: [
      { path: '', component: ListStub },
      { path: 'same', component: SectionStub, title: 'Tenants' },
      { path: ':id', component: DetailStub, title: 'Tenant' },
    ],
  },
];

describe('PageHead', () => {
  const mount = async (inHead: boolean): Promise<HTMLElement> => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), { provide: PAGE_ACTIONS_IN_HEAD, useValue: inHead }],
    });
    await TestBed.inject(Router).navigateByUrl('/tenants/t1');
    const fixture = TestBed.createComponent(DetailStub);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  };

  it('in the hotel app it is the plain head: title, lead and the aside, no trail, no actions (the context bar has them)', async () => {
    const page = await mount(false);
    expect(page.querySelector('h1')?.textContent.trim()).toBe('Levant Hotels');
    expect(page.querySelector('.lead')?.textContent.trim()).toBe('Jordan · JOD');
    expect(page.querySelector('.aside')?.textContent).toBe('Tabs');
    expect(page.querySelector('.ph-crumb')).toBeNull();
    expect(page.querySelector('.ph-acts')).toBeNull();
  });

  it('in the console it draws the trail above the title and the actions beside it: the offered template and the projected ones', async () => {
    const page = await mount(true);
    const crumb = page.querySelector('.ph-crumb');
    expect(crumb?.querySelector('a')?.textContent.trim()).toBe('Tenants');
    expect(crumb?.querySelector('a')?.getAttribute('href')).toBe('/tenants');
    expect(crumb?.querySelector('[aria-current="page"]')?.textContent.trim()).toBe('Levant Hotels');
    expect(page.querySelector('.ph-acts .pbtn')?.textContent).toBe('Edit');
    expect(page.querySelector('.ph-acts .extra')?.textContent).toBe('Invite');
    expect(page.querySelector('.aside')?.textContent).toBe('Tabs');
  });

  it('the heading is the title alone, the tile and the chips stand beside it, and a page named after its section shows no trail', async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), { provide: PAGE_ACTIONS_IN_HEAD, useValue: true }],
    });
    await TestBed.inject(Router).navigateByUrl('/tenants/same');
    const fixture = TestBed.createComponent(SectionStub);
    fixture.detectChanges();
    await fixture.whenStable();
    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelector('h1')?.textContent.trim()).toBe('Tenants');
    expect(page.querySelector('h1 .tile')).toBeNull();
    expect(page.querySelector('.ph-before .tile')?.textContent).toBe('T');
    expect(page.querySelector('.ph-title .chip')?.textContent).toBe('Active');
    expect(page.querySelector('.ph-crumb')).toBeNull();
  });
});
