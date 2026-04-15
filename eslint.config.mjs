import tsEslintPlugin from '@typescript-eslint/eslint-plugin';
import tsEslintParser from '@typescript-eslint/parser';

export default [
    {
        ignores: ['out/**', 'dist/**', '**/*.d.ts']
    },
    {
        files: ['src/**/*.ts'],
        languageOptions: {
            parser: tsEslintParser,
            ecmaVersion: 6,
            sourceType: 'module'
        },
        plugins: {
            '@typescript-eslint': tsEslintPlugin
        },
        rules: {
            '@typescript-eslint/naming-convention': 'warn',
            curly: 'warn',
            eqeqeq: 'warn',
            'no-throw-literal': 'warn',
            semi: 'warn'
        }
    }
];
