import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { registerInstrumentations } from '@opentelemetry/instrumentation';
import { DocumentLoadInstrumentation } from '@opentelemetry/instrumentation-document-load';
import { WebTracerProvider, SimpleSpanProcessor } from '@opentelemetry/sdk-trace-web';
import { resourceFromAttributes } from '@opentelemetry/resources';
import { SemanticResourceAttributes  } from '@opentelemetry/semantic-conventions';
import { FetchInstrumentation } from '@opentelemetry/instrumentation-fetch';
import { trace } from '@opentelemetry/api';

const exporter = new OTLPTraceExporter({
  url: 'http://collector.local/v1/traces',
});

const provider = new WebTracerProvider({
  resource: resourceFromAttributes({
    [SemanticResourceAttributes.SERVICE_NAME]: 'frontend',
  }),
  spanProcessors: [new SimpleSpanProcessor(exporter)]
});

provider.register();

const fetchInstrumentation = new FetchInstrumentation({
  propagateTraceHeaderCorsUrls: /.*/,
  applyCustomAttributesOnSpan: (span, request, result) => {
    console.log('request:', request);
    console.log('status:', result?.status);
    if (result instanceof Response && result.status >= 400) {
      span.setStatus({ code: 2, message: `HTTP ${result.status}` });
      span.setAttribute('error', true);
    }
  }
})

registerInstrumentations({
  instrumentations: [new DocumentLoadInstrumentation(), fetchInstrumentation]
});

const tracer = trace.getTracer('frontend');

// Catch synchronous errors
window.addEventListener('error', (event) => {
  const span = tracer.startSpan('unhandled-error');
  span.recordException(event.error || new Error(event.message));
  span.setStatus({ code: 2, message: event.message });
  span.end();
});

// Catch unhandled promise rejections
window.addEventListener('unhandledrejection', (event) => {
  const span = tracer.startSpan('unhandled-rejection');
  span.recordException(event.reason);
  span.setStatus({ code: 2, message: String(event.reason) });
  span.end();
});

console.log('OpenTelemetry instrumentation registered');