# WEB6 Final Frontend Pin Implementation Plan

> Execute as the approved WEB6 source-correction cycle. This plan does not authorize a live attempt.

1. Change pin tests to admit frontend `78e24ef00738f0b1d09ff466991802a736c67fec` and reject `76b125d74769b96be1f007803954b5b2489b10c3`.
2. Change the paired-source test to require tree `72a8e252f164c131fb72595cfda52c1432c2027c` and run the focused tests to capture the expected inverse RED.
3. Change only `PreparedCandidateReceipt.ReviewedFrontendSha` in production behavior and update candidate evidence.
4. Run the exact pin matrix, paired-source test, bounded integrated pure selection, locked Release/net48 build, and `git diff --check`.
5. Obtain independent review, commit, push PR #109, and wait for protected CI on the exact commit.
6. Stop before live preparation and request a fresh authorization for the newly verified backend/frontend source pair.
