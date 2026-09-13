# Frontend code style

Канон фронтенд-стилю для **dochub-site** (Cloudflare Pages + React + TypeScript).

**Джерело:** Doc Hub `Docs/frontend-code-style.md` ← Finance / ai-assistant.  
**Стан:** прийнято як цільовий DX; інтеграція **поступова** (див. §9).  
**Поруч:** [ADR 0001](ADR/0001-view-scoped-zustand-hybrid.md) — view-scoped Zustand для UI state.

**Мета:** той самий DX, що у Doc Hub — папки-компоненти, SCSS modules, BEM, `classnames/bind`, Prettier.

---

## 0. Поточний стан сайту (базовий)

| Зараз | Ціль (цей документ) |
| ----- | ------------------- |
| Глобальний `src/index.css` + utility-ish class names у JSX | SCSS modules + BEM + `classnames/bind` для **нового** UI |
| Плоскі файли `src/components/*.tsx` | Одна папка = один компонент + `index.ts` + `*.module.scss` |
| Page UI у `useState` | View UI → ADR 0001 factory + Provider; i18n / router лишаються |
| Немає Prettier / classnames / sass / zustand | Додати при першому міграційному PR (чекліст §9) |

**Правило інтеграції:** новий або суттєво розбитий UI пишемо вже під цей канон. Старий `index.css` не переписуємо «за компанію» — лише коли чіпаємо той самий компонент.

---

## 1. Prettier

| Option            | Value                    |
| ----------------- | ------------------------ |
| `semi`            | `true`                   |
| `tabWidth`        | `2`                      |
| `useTabs`         | `false`                  |
| `singleQuote`     | `false` (подвійні лапки) |
| `trailingComma`   | `"es5"`                  |
| `printWidth`      | `120`                    |
| `bracketSpacing`  | `true`                   |
| `bracketSameLine` | `false`                  |
| `arrowParens`     | `"always"`               |
| `endOfLine`       | `"lf"`                   |

Конфіг: `prettier.config.cjs` — ті самі правила, що Doc Hub / Finance.

---

## 2. Структура компонента

```text
components/
  ui/
    Card/
      Card.tsx
      Card.module.scss
      index.ts
  SocialLinks/
    SocialLinks.tsx
    SocialLinks.module.scss
    index.ts
pages/
  benchmark/
    BenchmarkPage.tsx          # або pages/BenchmarkPage.tsx + colocated store
    benchmarkUiStore.ts        # ADR 0001
    BenchmarkStoreContext.tsx
```

Імпорт ззовні — через barrel:

```ts
import { SocialLinks } from "components/SocialLinks";
```

---

## 3. TypeScript / React

- Функціональні компоненти; `FC<Props>` або явний return.
- Named export: `export { SocialLinks };`
- `classnames` + bind (§5).
- View UI state — [ADR 0001](ADR/0001-view-scoped-zustand-hybrid.md), не новий singleton у `src/stores/`.

---

## 4. SCSS architecture

### Глобальні стилі

Токени **вже** в `src/index.css` (`:root` → `--ink`, `--foam`, `--accent`, …).  
Нові модулі беруть SCSS-аліаси з `src/styles/abstracts/_variables.scss` → `var(--…)`, без другої палітри hex.

```text
src/styles/
  index.scss              # тонкий entry (опційно)
  abstracts/
    _variables.scss
    _mixins.scss
```

Підключення: `main.tsx` може імпортувати `./styles/index.scss` перед `./index.css` лише якщо з’являться shared base rules. Поки модулі `@use "styles/abstracts/…"`.

### CSS Modules

- Файл: `ComponentName.module.scss` поруч з `ComponentName.tsx`.
- Кореневий клас = **PascalCase**: `.SocialLinks`, `.Card`.
- Елементи — **BEM** через `&__`; модифікатори — `&.footer`, `&.band`.

```scss
@use "styles/abstracts/variables" as v;
@use "styles/abstracts/mixins" as m;

.SocialLinks {
  margin: 0;

  &__lede {
    color: v.$mute;
  }

  &.footer {
    /* …
```

### Що не робити

- Не тримати весь новий UI в одному `index.css`.
- Не змішувати legacy global class keys і BEM module keys в одному новому компоненті без причини.
- Не дублювати theme hex у модулях — тільки abstracts → CSS vars.

---

## 5. classnames / bind

```tsx
import classNames from "classnames/bind";
import scss from "./SocialLinks.module.scss";

const cn = classNames.bind(scss);

cn("SocialLinks", variant, className);
cn("SocialLinks__list");
```

Ключі — **рядки як у SCSS**, не `scss.SocialLinks`.

---

## 6. Імпорти

1. React / пакети  
2. Абсолютні аліаси (`components/…`, `styles/…`, `pages/…`, `lib/…`, `i18n/…`)  
3. Відносні  
4. `*.module.scss`

| Alias          | Path               |
| -------------- | ------------------ |
| `components/*` | `src/components/*` |
| `styles/*`     | `src/styles/*`     |
| `pages/*`      | `src/pages/*`      |
| `lib/*`        | `src/lib/*`        |
| `i18n/*`       | `src/i18n/*`       |

---

## 7. Файли / іменування

| Що                | Як                                         |
| ----------------- | ------------------------------------------ |
| Компонент / папка | `PascalCase`                               |
| Хуки              | `useSomething.ts`                          |
| Утиліти           | `camelCase.ts`                             |
| CSS module        | `Name.module.scss`                         |
| View UI store     | `xUiStore.ts` + `XStoreContext.tsx` (ADR 0001) |

---

## 8. Відхилення для сайту (свідомі)

- **Токени** — існуюча landing-палітра в `index.css` (`--ink` / Syne / DM Sans); не копіювати Doc Hub `--dh-*` і не Searates purple.
- **Немає Tailwind** на сайті — legacy = великий `index.css`; ціль = SCSS modules.
- **Admin** (`admin/`, `vite.admin.config.ts`) — той самий канон, коли чіпаємо; не обов’язково в першому PR.
- **i18n / Router** — chrome providers; виняток з ADR 0001 anti-singleton.

---

## 9. Чекліст міграції (сайт)

- [x] Додати `prettier` + `prettier.config.cjs`, `classnames`, `sass`, `zustand` (`npm run format`).
- [x] Завести `src/styles/abstracts` (аліаси на CSS vars з `index.css`).
- [x] Path aliases у Vite + tsconfig.
- [x] Перший пілотний folder-компонент: `components/SocialLinks`.
- [x] Перший ADR 0001 view store: Benchmark tabs.
- [x] Наступні sections: home bands through Download/Lightbox (`Features`…`Screens`, `ProductStage`, `ControlledWorkflow`, `DownloadBand`/`DownloadCtas`, `Lightbox`).
- [x] `WhatsNew` + `SiteFooter`.
- [x] `Seo` / `Analytics` folder components.
- [x] `pages/Docs` (DocsPage folder + SCSS module; article chrome no longer via `.bench-*`).
- [x] `pages/benchmark` — `Benchmark.module.scss` scoped under `.Benchmark` (`:global` `.bench-*` until BEM pass); page barrel + Provider.
- [ ] Лишились: shared `.btn`/page CSS, layout hooks (`.page`/`.band`); optional BEM rewrite of `.bench-*` JSX.
- [ ] Не переписувати весь `index.css` у тому ж PR.

---

## 10. Приклад

Див. `src/components/SocialLinks/` після пілоту — BEM + `classnames/bind` + tokens з abstracts.