import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./app/**/*.{js,ts,jsx,tsx,mdx}",
    "./components/**/*.{js,ts,jsx,tsx,mdx}",
    "./lib/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        canvas: "#f7f6f2",
        ink: "#131313",
        muted: "#5e5d58",
        line: "#e5e2d9",
        accent: "#1f2937",
      },
      fontFamily: {
        sans: ["var(--font-manrope)", "sans-serif"],
        serif: ["var(--font-literata)", "serif"],
      },
      boxShadow: {
        soft: "0 1px 2px rgba(17, 24, 39, 0.05)",
      },
    },
  },
  plugins: [],
};

export default config;
