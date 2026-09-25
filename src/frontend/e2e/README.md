# Browser integration tests

This suite runs the Angular application against a real API and PostgreSQL database. It covers the critical authentication, sale lifecycle, concurrency, navigation, resilience, mobile, keyboard, and accessibility paths.

## Scenario coverage

| Scenario | Automated evidence                                                                                                         |
| -------- | -------------------------------------------------------------------------------------------------------------------------- |
| E01      | Create four units and verify the 10% discount in the UI and API.                                                           |
| E02      | Move from three to ten units and verify the persisted discount tier.                                                       |
| E03      | Reject an excessive quantity and duplicate products in both UI and direct API calls.                                       |
| E04      | Cancel one item and then the final item while retaining history and reaching a zero total.                                 |
| E05      | Repeat a full-sale cancellation without duplicating its effect.                                                            |
| E06      | Delete the only result on the last page, correct pagination, and confirm API absence.                                      |
| E07      | Submit two edits with the same ETag and preserve the losing session's draft.                                               |
| E08      | Preserve filter, order, size, and page through detail navigation.                                                          |
| E09      | Use a genuinely expired signed JWT and verify one login redirect and one write attempt.                                    |
| E10      | Interrupt the create response after persistence, avoid an automatic retry, and find one sale by number.                    |
| E11      | Check a mobile viewport, labels, invalid-field focus, keyboard activation, confirmation focus, and serious axe violations. |
| E12      | Protect a direct route and restore it after login both before and after a full reload.                                     |

## Prerequisites

Install Chromium once, then run the isolated stack:

```powershell
npm run test:e2e:install
npm run test:e2e
```

The command builds and starts disposable API and PostgreSQL containers, generates credentials in memory, runs the browser suite, and removes containers and volumes in a `finally` block.

Arguments after `--` are forwarded to Playwright. For example, this command repeats the interrupted-response scenario without retries while reusing one isolated stack:

```powershell
npm run test:e2e -- resilience-accessibility.spec.ts --grep E10 --repeat-each=10 --retries=0
```

CI retries unexpected browser failures to retain diagnostic evidence, but `failOnFlakyTests` makes the job fail when any test needs a retry.

To test an already running stack, configure an active `Manager` or `Admin` account outside Git and run:

```powershell
$env:E2E_ADMIN_EMAIL = 'admin@example.com'
$env:E2E_ADMIN_PASSWORD = '<local-password>'
$env:E2E_SKIP_WEB_SERVER = '1' # omit when Angular should be started automatically
npm run test:e2e:existing
```

The Angular development server starts automatically. Set `E2E_BASE_URL` when testing another frontend URL. The suite authenticates once per Playwright worker and reuses that real response in isolated browser contexts so it respects the API login rate limit. Unique sale identities make test runs independent of execution order. Reports, traces, screenshots, and videos are ignored by Git; failure artifacts are retained by Playwright.
