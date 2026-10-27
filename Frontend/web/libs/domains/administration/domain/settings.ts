/** How a key fared at a hotel (Identity's ResolutionOutcome). */
export type SettingOutcome = 'Value' | 'Unset' | 'Conflict' | 'NotApplicable';

/** Where a step of the trace looked (Identity's ResolutionLevel). */
export type SettingLevel = 'Property' | 'PropertyGroup' | 'Tenant' | 'SystemDefault';

/** How one place fared (Identity's ResolutionVerdict). */
export type SettingVerdict = 'Won' | 'Overridden' | 'NotSet' | 'Tied' | 'Outranked' | 'Inactive';

/** One line of a trace (ResolutionStepModel): a place, what it holds for the key, how it fared. */
export interface ResolutionStep {
  readonly level: SettingLevel;
  /** The hotel's or the group's id; null for the customer and the default. */
  readonly id: string | null;
  readonly code: string | null;
  readonly precedence: number | null;
  readonly value: string | null;
  readonly verdict: SettingVerdict;
}

/** One row of GET /api/ConfigValue/effective (EffectiveSettingModel): a key resolved at the hotel the call names. */
export interface EffectiveSetting {
  readonly key: string;
  readonly ownerService: string;
  /** time, int, bool, decimal or string: a hint for the input, never checked by Identity. */
  readonly dataType: string;
  readonly outcome: SettingOutcome;
  readonly value: string | null;
  readonly source: SettingLevel | null;
  readonly sourceCode: string | null;
  readonly trace: readonly ResolutionStep[];
}

/** Where a value is set, as the value doors name it (ConfigValue Set/Clear). */
export type SettingScope = 'Tenant' | 'PropertyGroup' | 'Property';

/** One place a person can set a key at, from the trace: the hotel, one of its groups, or the whole customer. */
export interface SettingPlace {
  readonly scope: SettingScope;
  /** The hotel's or the group's id; null for the whole customer. */
  readonly scopeId: string | null;
  readonly code: string | null;
  /** What is set there now; null when nothing. */
  readonly value: string | null;
}

/** Which levels of a trace can hold a value, and under which scope the value doors name them; the default cannot. */
const scopeOfLevel: Readonly<Record<SettingLevel, SettingScope | null>> = {
  Property: 'Property',
  PropertyGroup: 'PropertyGroup',
  Tenant: 'Tenant',
  SystemDefault: null,
};

/** The places of a trace that can hold a value, in the trace's order (the default is the registry's, never set here). */
export const placesOf = (setting: EffectiveSetting): readonly SettingPlace[] =>
  setting.trace.flatMap((step) => {
    const scope = scopeOfLevel[step.level];
    return scope === null
      ? []
      : [
          {
            scope,
            scopeId: scope === 'Tenant' ? null : step.id,
            code: step.code,
            value: step.value,
          },
        ];
  });
