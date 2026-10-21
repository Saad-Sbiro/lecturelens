export const languages = {
  fr: { code: 'fr', nativeName: 'Français', englishName: 'French', script: 'Latn', direction: 'ltr', enabled: true, status: 'experimental', speechCode: 'fr', textCode: 'fra', fontStack: 'Arial, sans-serif', promptLocale: 'fr', providers: [] },
  en: { code: 'en', nativeName: 'English', englishName: 'English', script: 'Latn', direction: 'ltr', enabled: true, status: 'experimental', speechCode: 'en', textCode: 'eng', fontStack: 'Arial, sans-serif', promptLocale: 'en', providers: [] },
} as const;

export type LanguageCode = keyof typeof languages;
export const defaultLanguages: { teacher: LanguageCode; output: LanguageCode; ui: LanguageCode } = {
  teacher: 'fr', output: 'fr', ui: 'en',
};
