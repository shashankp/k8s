import { Component, type ReactNode } from 'react';
import { trace } from '@opentelemetry/api';

interface Props {
  children: ReactNode;
}

class ErrorBoundary extends Component<Props> {
  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    const tracer = trace.getTracer('frontend');
    const span = tracer.startSpan('react-error');
    span.recordException(error);
    span.setAttribute('componentStack', errorInfo.componentStack || '');
    span.setStatus({ code: 2, message: error.message });
    span.end();
  }

  render() {
    return this.props.children;
  }
}

export default ErrorBoundary;
