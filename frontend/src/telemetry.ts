import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { registerInstrumentations } from '@opentelemetry/instrumentation';
import { DocumentLoadInstrumentation } from '@opentelemetry/instrumentation-document-load';
import { WebTracerProvider, SimpleSpanProcessor } from '@opentelemetry/sdk-trace-web';

const exporter = new OTLPTraceExporter({
  url: 'http://localhost:4318/v1/traces',
});

const provider = new WebTracerProvider({
  spanProcessors: [new SimpleSpanProcessor(exporter)]
});

provider.register();

registerInstrumentations({
  instrumentations: [new DocumentLoadInstrumentation()],
});