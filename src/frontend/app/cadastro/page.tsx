import { redirect } from "next/navigation";
import { auth } from "@/auth";
import { SignUpForm } from "@/components/auth/sign-up-form";

export default async function CadastroPage() {
  const session = await auth();

  if (session?.user?.id) {
    redirect("/");
  }

  return <SignUpForm />;
}
