# WEB6 resend eligibility v1 corpus

Additive source fixtures for the default-off pre-dispatch receipt and read-only eligibility boundary. Existing WEB4/WEB5 corpora are unchanged. `<receipt>` stands for the separately verified compact signed token.

The corpus contains one candidate-context fixture, six operation-kind receipt fixtures, and seven eligibility fixtures covering the only allowed decisions/reasons. Receipt signatures and semantic bindings are verified separately by `Web6ResendReceiptTests`; endpoint serialization and property order are verified by `Web6ResendEligibilityEndpointTests`. These fixtures do not authorize or simulate a business mutation.
