import { Suspense } from "react";
import { VerifyEmailPanel } from "@/components/auth/verify-email-panel";

export default function VerificarEmailPage() {
  return (
    <Suspense fallback={<p className="text-sm text-muted">Carregando...</p>}>
      <VerifyEmailPanel />
    </Suspense>
  );
}
