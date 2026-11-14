# WEB6 Final Frontend Pin Design

## Scope

Advance the backend candidate bootstrap reviewed-frontend pin from `76b125d74769b96be1f007803954b5b2489b10c3` to the exact reviewed frontend correction `78e24ef00738f0b1d09ff466991802a736c67fec`, tree `72a8e252f164c131fb72595cfda52c1432c2027c`.

## Behavior

- Admit only the new exact frontend SHA through `PreparedCandidateReceipt.ReviewedFrontendSha`.
- Reject the superseded frontend SHA with `BLOCKED_REVIEWED_FRONTEND_PIN`.
- Keep the existing clean-tree, tracked-source, deadline hierarchy, selector, and allocation fences unchanged.
- Update paired-source evidence to identify the exact reviewed commit and tree.

## Verification

Use an inverse RED before production modification, then run the exact pin matrix and paired-source test against the reviewed frontend checkout. Follow with the bounded integrated pure selection and locked Release/net48 build. Protected backend CI must pass on the exact backend commit before another live authorization can be requested.

No SQL, IIS, browser-live candidate, approval file, or retained resource is created by this correction.
