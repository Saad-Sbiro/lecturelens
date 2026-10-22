import registry from './languages.json';
export const languages = registry;
export type LanguageCode = keyof typeof languages;
const defaultRole = (role: 'defaultTeacher' | 'defaultOutput' | 'defaultUi') => (Object.keys(languages) as LanguageCode[]).find(code => languages[code][role])!;
export const defaultLanguages: { teacher: LanguageCode; output: LanguageCode; ui: LanguageCode } = { teacher: defaultRole('defaultTeacher'), output: defaultRole('defaultOutput'), ui: defaultRole('defaultUi') };
