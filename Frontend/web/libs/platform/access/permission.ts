/**
 * The platform permission catalogue, mirrored from the framework's PermissionEnum
 * (Harmony.Framework/Harmony.Core.Identity/Permissions/PermissionEnum.cs, 14 values on 24 October 2026).
 * A number is the stored value: it never changes and is never reused; append only. A retired permission keeps its
 * number here as well. check-error-codes will one day compare this file with the enum (D25).
 */
const VIEW_LOOKUPS = 1;
const MANAGE_LOOKUPS = 2;
const VIEW_ROLES = 3;
const CREATE_ROLE = 4;
const EDIT_ROLE = 5;
const DELETE_ROLE = 6;
const MANAGE_ROLE_PERMISSIONS = 7;
const ASSIGN_USERS_TO_ROLE = 8;
const VIEW_USERS = 9;
const MANAGE_USERS = 10;
const VIEW_TENANTS = 11;
const MANAGE_TENANTS = 12;
const VIEW_PROPERTIES = 13;
const MANAGE_PROPERTIES = 14;

export const Permission = {
  // Lookups
  ViewLookups: VIEW_LOOKUPS,
  ManageLookups: MANAGE_LOOKUPS,
  // Roles and permissions
  ViewRoles: VIEW_ROLES,
  CreateRole: CREATE_ROLE,
  EditRole: EDIT_ROLE,
  DeleteRole: DELETE_ROLE,
  ManageRolePermissions: MANAGE_ROLE_PERMISSIONS,
  AssignUsersToRole: ASSIGN_USERS_TO_ROLE,
  // Users
  ViewUsers: VIEW_USERS,
  ManageUsers: MANAGE_USERS,
  // Platform
  ViewTenants: VIEW_TENANTS,
  ManageTenants: MANAGE_TENANTS,
  // Properties
  ViewProperties: VIEW_PROPERTIES,
  ManageProperties: MANAGE_PROPERTIES,
} as const;

export type Permission = (typeof Permission)[keyof typeof Permission];
