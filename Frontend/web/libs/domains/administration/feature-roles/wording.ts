import { PrivilegeLevel } from '@harmony/administration/domain';

/** The permissions in the person's language, keyed by the PermissionEnum member's name Identity sends. */
const byPermission: Readonly<Record<string, string>> = {
  ViewLookups: $localize`:@@administration.permission.view-lookups:View lookups`,
  ManageLookups: $localize`:@@administration.permission.manage-lookups:Manage lookups`,
  ViewRoles: $localize`:@@administration.permission.view-roles:View roles`,
  CreateRole: $localize`:@@administration.permission.create-role:Create roles`,
  EditRole: $localize`:@@administration.permission.edit-role:Edit roles`,
  DeleteRole: $localize`:@@administration.permission.delete-role:Delete roles`,
  ManageRolePermissions: $localize`:@@administration.permission.manage-role-permissions:Manage role permissions`,
  AssignUsersToRole: $localize`:@@administration.permission.assign-users-to-role:Assign roles to people`,
  ViewUsers: $localize`:@@administration.permission.view-users:View users`,
  ManageUsers: $localize`:@@administration.permission.manage-users:Manage users`,
  ViewTenants: $localize`:@@administration.permission.view-tenants:View tenants`,
  ManageTenants: $localize`:@@administration.permission.manage-tenants:Manage tenants`,
  ViewProperties: $localize`:@@administration.permission.view-properties:View properties`,
  ManageProperties: $localize`:@@administration.permission.manage-properties:Manage properties`,
  ViewSettings: $localize`:@@administration.permission.view-settings:View settings`,
  ManageSettings: $localize`:@@administration.permission.manage-settings:Manage settings`,
};

/** A permission's label; a member the frontend does not know yet shows the name Identity sent. */
export const permissionLabel = (value: string, fallback: string): string =>
  byPermission[value] ?? fallback;

const byLevel: Readonly<Record<PrivilegeLevel, string>> = {
  [PrivilegeLevel.standard]: $localize`:@@administration.level.standard:Standard`,
  [PrivilegeLevel.supervisor]: $localize`:@@administration.level.supervisor:Supervisor`,
  [PrivilegeLevel.manager]: $localize`:@@administration.level.manager:Manager`,
  [PrivilegeLevel.administrator]: $localize`:@@administration.level.administrator:Administrator`,
};

export const privilegeLabel = (level: PrivilegeLevel): string => byLevel[level];
