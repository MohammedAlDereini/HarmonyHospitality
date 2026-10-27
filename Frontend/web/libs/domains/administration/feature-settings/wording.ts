import {
  EffectiveSetting,
  ResolutionStep,
  SettingLevel,
  SettingOutcome,
  SettingPlace,
  SettingScope,
  SettingVerdict,
} from '@harmony/administration/domain';

/** Where a value came from, in the person's words, by the level that won. */
const bySource: Readonly<Record<SettingLevel, (code: string) => string>> = {
  Property: () => $localize`:@@administration.settings.source.hotel:This hotel`,
  PropertyGroup: (code) => $localize`:@@administration.settings.source.group:Group ${code}:CODE:`,
  Tenant: () => $localize`:@@administration.settings.source.customer:Whole customer`,
  SystemDefault: () => $localize`:@@administration.settings.source.default:Default`,
};

export const sourceLabel = (setting: EffectiveSetting): string =>
  setting.source === null ? '' : bySource[setting.source](setting.sourceCode ?? '');

/** What a setting is at the hotel when no value won; null when one did. */
const byOutcome: Readonly<Record<SettingOutcome, string | null>> = {
  Value: null,
  Unset: $localize`:@@administration.settings.outcome.unset:Not set`,
  Conflict: $localize`:@@administration.settings.outcome.conflict:Two groups disagree`,
  NotApplicable: $localize`:@@administration.settings.outcome.not-applicable:Not for this kind of property`,
};

export const outcomeLabel = (setting: EffectiveSetting): string | null =>
  byOutcome[setting.outcome];

/** One place of a trace, in words. */
const byStep: Readonly<Record<SettingLevel, (step: ResolutionStep) => string>> = {
  Property: (step) =>
    $localize`:@@administration.settings.step.hotel:This hotel (${step.code ?? ''}:CODE:)`,
  PropertyGroup: (step) =>
    $localize`:@@administration.settings.step.group:Group ${step.code ?? ''}:CODE:, precedence ${step.precedence ?? 0}:PRECEDENCE:`,
  Tenant: () => $localize`:@@administration.settings.step.customer:Whole customer`,
  SystemDefault: () => $localize`:@@administration.settings.step.default:Default`,
};

export const stepLabel = (step: ResolutionStep): string => byStep[step.level](step);

const byVerdict: Readonly<Record<SettingVerdict, string>> = {
  Won: $localize`:@@administration.settings.verdict.won:Wins`,
  Overridden: $localize`:@@administration.settings.verdict.overridden:Overridden`,
  NotSet: $localize`:@@administration.settings.verdict.not-set:Not set`,
  Tied: $localize`:@@administration.settings.verdict.tied:Tied`,
  Outranked: $localize`:@@administration.settings.verdict.outranked:Outranked`,
  Inactive: $localize`:@@administration.settings.verdict.inactive:Group switched off`,
};

export const verdictLabel = (verdict: SettingVerdict): string => byVerdict[verdict];

/** A place a value can be set at, in words: the choices of the editor. */
const byPlace: Readonly<Record<SettingScope, (code: string) => string>> = {
  Property: (code) => $localize`:@@administration.settings.place.hotel:This hotel (${code}:CODE:)`,
  PropertyGroup: (code) => $localize`:@@administration.settings.place.group:Group ${code}:CODE:`,
  Tenant: () => $localize`:@@administration.settings.place.customer:Whole customer`,
};

export const placeLabel = (place: SettingPlace): string => byPlace[place.scope](place.code ?? '');
