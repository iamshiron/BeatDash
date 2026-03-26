export default {
	root: true,
	ignores: ["dist", "routeTree.gen.ts"],
	plugins: ["@typescript-eslint", "react-hooks", "react-refresh"],
	extends: [
		"eslint:recommended",
		"plugin:@typescript-eslint/recommended",
		"plugin:react-hooks/recommended",
	],
	rules: {
		"react-refresh/only-export-components": [
			"warn",
			{ allowConstantExport: true },
		],
		"@typescript-eslint/no-unused-vars": ["error", { argsIgnorePattern: "^_" }],
	},
};
