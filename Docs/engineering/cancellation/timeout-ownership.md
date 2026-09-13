# Timeout Ownership

Timeouts must have explicit owner. Provider request timeout → provider execution concern. Capacity wait budget → routing/capacity concern. Semantic policy timeout → supervision concern. HTTP/client cancellation → caller/request cancellation. Do NOT hide arbitrary timeouts in utility/helper methods.
