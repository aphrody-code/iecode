# Inazuma Eleven Ecosystem - AI Coding Instructions

## 1. Project Context & Architecture
- **Monorepo Structure**: Managed with `pnpm` workspaces.
  - `apps/azalee`: Main Next.js 16 web application.
  - `packages/inagle`: Core Game Data API (TypeScript).
  - `packages/nie-parser`: AI-friendly parser for decompiled game code.
  - `data/`: Raw game data (JSON exports from `cfg.bin`).
- **Tech Stack**:
  - **Frontend**: Next.js 16 (App Router), React 19, Tailwind CSS v4.
  - **UI Components**: Radix UI primitives, Framer Motion.
  - **Data**: `@azalee/inagle` (Local JSON-based API), Supabase (Backend).
  - **Package Manager**: `pnpm` (Use `pnpm -r` for recursive commands).

## 2. Critical Rule: Official Data Only
All data extraction and content generation MUST rely EXCLUSIVELY on official sources.
- **Official Sources**: `*.inazuma.jp`, `cfg.bin` exports in `dump/data/common/gamedata/`.
- **Forbidden**: Third-party wikis, fan-made spreadsheets, or unverified community data.
- **Verification**: If data isn't in `packages/inagle` or `data/`, it likely doesn't exist or isn't verified.

## 3. Data Access: `@azalee/inagle`
**ALWAYS** use this package to access game data. **NEVER** import raw JSON files directly in `apps/azalee`.

### Usage Patterns
```typescript
import { createBasaraAPI, createSkillsAPI, createCharactersAPI } from '@azalee/inagle';

// Initialize APIs
const basara = createBasaraAPI();
const skills = createSkillsAPI();

// Fetch Data
const fireStrikers = basara.forwards().filter(c => c.element === 'Feu');
const strongSkills = skills.strongest(10);

// Search (Multilingual)
const results = basara.search('Shuya', { lang: 'fr' });
```

### Key APIs
- **Basara**: `createBasaraAPI()` - Legendary characters (Stats, Builds, Variants).
- **Skills**: `createSkillsAPI()` - Hissatsu, Aura, and Passive skills.
- **Characters**: `createCharactersAPI()` - General character data.
- **Teams**: `createTeamsAPI()` - Teams and Formations.
- **Global Search**: `createGlobalSearch()` - Unified fuzzy search across all data.

## 4. Frontend Development (`apps/azalee`)
### Core Conventions
- **Next.js 16**: Use Server Components by default. Use `'use client'` only when interactivity is needed.
- **Styling**: Tailwind CSS v4. Use `clsx` and `tailwind-merge` (via `cn` utility if available) for class composition.
- **Icons**: Use **Material Symbols** (Rounded variant) for system UI.
- **Visuals**: **NO EMOJIS**. **NO GENERIC LOADERS**. Use only official Victory Road icons/assets located in `public/`.
- **Forms**: `react-hook-form` + `zod` for validation.
- **Charts**: `recharts` for data visualization.

### Directory Structure
- `app/`: App Router pages and layouts.
- `components/`: Reusable UI components (Radix + Tailwind).
- `lib/`: Utility functions.
- `hooks/`: Custom React hooks.

### Design & Motion
- **Material Design 3**: Follow M3 guidelines.
- **Motion**: Use the [Motion Physics System](https://m3.material.io/styles/motion/overview/how-it-works).
  - Prefer **Spring Physics** over duration/easing.
  - Use **Expressive** scheme for hero moments, **Standard** for utilitarian interactions.
  - Implement using `framer-motion` springs (stiffness/damping) to match M3 tokens.

## 5. Code Analysis: `@azalee/nie-parser`
Use this package to understand game mechanics from decompiled code.
```typescript
import { searchFunctions, generateContext } from '@azalee/nie-parser';

// Search for mechanics
const networkFuncs = searchFunctions(db, "socket connect");
```

## 6. Automation & MCP Tools (Playwright)
Use these tools for official site automation and data extraction.

### Navigation & Extraction
- `take_snapshot`: **(Primary)** Get A11y tree for element ID.
- `evaluate_script`: Execute JS in page context (refer to `inagle/src/web/inazuma-api.ts`).
- `list_network_requests`: Monitor CDN requests (`dxi4wb638ujep.cloudfront.net`).

### Game Data Structure
- **Root**: `dump/data/common/gamedata/`
- **Format**: Binary `cfg.bin` files exported to JSON.
- **Priority**: `cfg.bin` > Site Scraping.

## 7. Workflow
- **Start Dev Server**: `pnpm dev` (Runs all apps/packages in watch mode).
- **Build**: `pnpm build`.
- **Lint**: `pnpm lint`.

## 8. Documentation & External Resources
- **Context7 MCP**: ALWAYS use the `context7` MCP tool to fetch up-to-date documentation for:
  - **Next.js 16**: Use library ID `/vercel/next.js` (or resolve via `resolve-library-id`).
  - **Material Design 3**: Use library ID `/material-design/material-web` or similar (resolve via `resolve-library-id`).
- **Reasoning**: Ensure usage of the latest features and patterns for these specific frameworks.
