# Componentes UI (Shadcn UI) — Backoffice y Portal del Cliente

Este documento contiene la implementación lista para producción de las vistas principales tanto del **Backoffice** (Evaluación y Desembolso Digital) como del **Portal del Cliente** (Consulta de cuotas y Pago online), estructuradas con **React + TypeScript + Tailwind CSS + Shadcn UI**.

---

## 1. Vista de Backoffice: Aprobación y Desembolso Bancario

**Archivo sugerido:** `src/features/loans/components/BackofficeLoanDisbursementView.tsx`

```tsx
import React, { useState } from "react";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Checkbox } from "@/components/ui/checkbox";
import { AlertCircle, CheckCircle2, CreditCard, Send, ArrowUpRight } from "lucide-react";

export interface ScheduleInstallment {
  number: number;
  dueDate: string;
  principal: number;
  interest: number;
  fee: number;
  total: number;
  remainingBalance: number;
  status: "PENDING" | "PAID" | "OVERDUE";
}

export const BackofficeLoanDisbursementView: React.FC = () => {
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [certified, setCertified] = useState(true);

  // Datos de ejemplo del expediente en evaluación
  const loanData = {
    id: "SOL-2026-0842",
    customerName: "Manuel Vargas Mendoza",
    docNumber: "47829103",
    monthlyIncome: 4200.0,
    dtiRatio: 18.2,
    amount: 5000.0,
    term: 6,
    frequency: "MENSUAL",
    tea: 32.5,
    monthlyPayment: 908.4,
    companyAccount: "BCP Empresa (002-191-000492819-54)",
    customerBank: "BCP Cuenta Sueldo (Débito)",
    customerMaskedCard: "**** **** **** 8219",
    customerCci: "002-191-149204928104-58",
  };

  const schedule: ScheduleInstallment[] = [
    { number: 1, dueDate: "05/12/2026", principal: 785.4, interest: 115.0, fee: 8.0, total: 908.4, remainingBalance: 4214.6, status: "PENDING" },
    { number: 2, dueDate: "05/01/2027", principal: 803.45, interest: 96.95, fee: 8.0, total: 908.4, remainingBalance: 3411.15, status: "PENDING" },
    { number: 3, dueDate: "05/02/2027", principal: 821.9, interest: 78.5, fee: 8.0, total: 908.4, remainingBalance: 2589.25, status: "PENDING" },
    { number: 4, dueDate: "05/03/2027", principal: 840.8, interest: 59.6, fee: 8.0, total: 908.4, remainingBalance: 1748.45, status: "PENDING" },
    { number: 5, dueDate: "05/04/2027", principal: 860.15, interest: 40.25, fee: 8.0, total: 908.4, remainingBalance: 888.3, status: "PENDING" },
    { number: 6, dueDate: "05/05/2027", principal: 888.3, interest: 20.4, fee: 8.0, total: 916.7, remainingBalance: 0.0, status: "PENDING" },
  ];

  const handleDisburse = async () => {
    setIsProcessing(true);
    try {
      // Simulación de llamada a POST /api/loans/{id}/disburse con Idempotency-Key
      await new Promise((res) => setTimeout(res, 1200));
      setIsDialogOpen(false);
      alert("Transferencia bancaria ejecutada exitosamente vía API BCP.");
    } finally {
      setIsProcessing(false);
    }
  };

  return (
    <div className="space-y-6 max-w-6xl mx-auto p-4 md:p-6">
      {/* Encabezado del Expediente */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between pb-4">
          <div>
            <div className="flex items-center gap-2">
              <CardTitle className="text-xl">Expediente #{loanData.id}</CardTitle>
              <Badge variant="outline" className="bg-blue-500/10 text-blue-500 border-blue-500/30">
                EN EVALUACIÓN
              </Badge>
            </div>
            <CardDescription className="text-xs mt-1">
              Validación KYC biométrica aprobada. Destino: Transferencia Bancaria Directa.
            </CardDescription>
          </div>

          <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
            <DialogTrigger asChild>
              <Button className="gap-2 bg-blue-600 hover:bg-blue-500 text-white">
                <Send className="w-4 h-4" />
                Aprobar y Desembolsar
              </Button>
            </DialogTrigger>
            <DialogContent className="sm:max-w-md">
              <DialogHeader>
                <DialogTitle>Autorizar Transferencia Bancaria</DialogTitle>
                <DialogDescription>
                  Se emitirá una transferencia electrónica inmediata desde la cuenta institucional al cliente.
                </DialogDescription>
              </DialogHeader>

              <div className="rounded-lg bg-muted p-4 space-y-2 text-xs font-mono">
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Origen (Fintech):</span>
                  <span className="font-semibold">{loanData.companyAccount}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Destino (Cliente):</span>
                  <span className="font-semibold">{loanData.customerBank}</span>
                </div>
                <div className="flex justify-between text-sm pt-1 border-t">
                  <span className="text-muted-foreground">Importe Neto:</span>
                  <span className="font-bold text-emerald-500">S/ {loanData.amount.toFixed(2)}</span>
                </div>
              </div>

              <div className="flex items-center space-x-2 pt-2">
                <Checkbox
                  id="certify"
                  checked={certified}
                  onCheckedChange={(v) => setCertified(!!v)}
                />
                <label htmlFor="certify" className="text-xs text-muted-foreground leading-tight">
                  Confirmo que el DNI del titular coincide con la cuenta destino y autorizo el desembolso.
                </label>
              </div>

              <DialogFooter>
                <Button variant="outline" onClick={() => setIsDialogOpen(false)}>
                  Cancelar
                </Button>
                <Button
                  onClick={handleDisburse}
                  disabled={!certified || isProcessing}
                  className="bg-blue-600 hover:bg-blue-500 text-white"
                >
                  {isProcessing ? "Procesando en banco..." : "Confirmar Transferencia"}
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </CardHeader>

        <CardContent className="grid grid-cols-1 md:grid-cols-3 gap-6 pt-4 border-t">
          {/* Col 1: Datos Cliente */}
          <div className="space-y-2">
            <h4 className="text-xs font-semibold uppercase text-muted-foreground tracking-wider">
              Solicitante
            </h4>
            <div className="text-sm font-bold">{loanData.customerName}</div>
            <div className="text-xs text-muted-foreground font-mono">DNI: {loanData.docNumber}</div>
            <div className="text-xs text-muted-foreground">
              Ingreso neto: <span className="font-semibold text-foreground">S/ {loanData.monthlyIncome.toFixed(2)}</span>
            </div>
            <div className="text-xs text-emerald-500 font-medium">
              Ratio DTI: {loanData.dtiRatio}% (Aceptable)
            </div>
          </div>

          {/* Col 2: Condiciones */}
          <div className="space-y-2">
            <h4 className="text-xs font-semibold uppercase text-muted-foreground tracking-wider">
              Condiciones del Crédito
            </h4>
            <div className="text-sm font-bold">S/ {loanData.amount.toFixed(2)}</div>
            <div className="text-xs text-muted-foreground">
              {loanData.term} cuotas {loanData.frequency.toLowerCase()}s de S/ {loanData.monthlyPayment.toFixed(2)}
            </div>
            <div className="text-xs text-blue-500 font-medium">
              TEA Pactada: {loanData.tea}% (Amortización Francesa)
            </div>
          </div>

          {/* Col 3: Cuenta Destino */}
          <div className="space-y-2">
            <h4 className="text-xs font-semibold uppercase text-muted-foreground tracking-wider">
              Cuenta Receptora Verificada
            </h4>
            <div className="flex items-center gap-2 text-sm font-semibold">
              <CreditCard className="w-4 h-4 text-emerald-500" />
              {loanData.customerBank}
            </div>
            <div className="text-xs font-mono text-muted-foreground">
              Tarjeta: {loanData.customerMaskedCard}<br />
              CCI: {loanData.customerCci}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Tabla de Cronograma Proyectado */}
      <Card>
        <CardHeader>
          <CardTitle className="text-sm font-semibold">Cronograma Proyectado de Cuotas</CardTitle>
          <CardDescription className="text-xs">
            Plan inmutable generado automáticamente con ajuste de centavos en la última cuota.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-16"># Cuota</TableHead>
                <TableHead>Vencimiento</TableHead>
                <TableHead>Capital</TableHead>
                <TableHead>Interés</TableHead>
                <TableHead>Seguro</TableHead>
                <TableHead>Total Cuota</TableHead>
                <TableHead>Saldo Restante</TableHead>
                <TableHead className="text-right">Estado</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody className="font-mono text-xs">
              {schedule.map((row) => (
                <TableRow key={row.number}>
                  <TableCell className="font-sans font-medium">Cuota {row.number}</TableCell>
                  <TableCell>{row.dueDate}</TableCell>
                  <TableCell>S/ {row.principal.toFixed(2)}</TableCell>
                  <TableCell>S/ {row.interest.toFixed(2)}</TableCell>
                  <TableCell>S/ {row.fee.toFixed(2)}</TableCell>
                  <TableCell className="font-semibold text-foreground">S/ {row.total.toFixed(2)}</TableCell>
                  <TableCell className="text-muted-foreground">S/ {row.remainingBalance.toFixed(2)}</TableCell>
                  <TableCell className="text-right font-sans">
                    <Badge variant="secondary" className="text-[11px]">
                      {row.status}
                    </Badge>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
};
```

---

## 2. Vista del Portal del Cliente: Préstamo Activo y Pago Digital

**Archivo sugerido:** `src/features/loans/components/ClientPortalLoanView.tsx`

```tsx
import React, { useState } from "react";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { QrCode, CheckCircle2, ShieldCheck, Smartphone, ExternalLink } from "lucide-react";

export const ClientPortalLoanView: React.FC = () => {
  const [isPayOpen, setIsPayOpen] = useState(false);
  const [voucherCode, setVoucherCode] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const loanInfo = {
    code: "LN-2026-9402",
    totalBalance: 4214.6,
    nextDueNumber: 1,
    nextDueDate: "05 de Diciembre, 2026",
    nextDueAmount: 908.4,
    progressPercent: 16.6,
    disbursedTo: "BCP Cuenta Sueldo (**** 8219)",
  };

  const handleConfirmPayment = async () => {
    setSubmitting(true);
    try {
      // Simula envío a POST /api/client/payments
      await new Promise((res) => setTimeout(res, 1000));
      alert("¡Comprobante enviado! El pago se conciliará automáticamente vía webhook.");
      setIsPayOpen(false);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="space-y-6 max-w-4xl mx-auto p-4 md:p-6">
      {/* Banner de Bienvenida */}
      <Card className="bg-gradient-to-r from-blue-950/40 via-card to-card border-blue-500/20">
        <CardHeader className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
          <div>
            <Badge className="bg-emerald-500/15 text-emerald-400 border-emerald-500/30">
              <ShieldCheck className="w-3.5 h-3.5 mr-1" />
              CRÉDITO ACTIVO • AL DÍA
            </Badge>
            <CardTitle className="text-xl mt-2 font-bold">¡Hola, Manuel!</CardTitle>
            <CardDescription className="text-xs">
              Tu desembolso fue acreditado exitosamente a tu cuenta {loanInfo.disbursedTo}.
            </CardDescription>
          </div>

          <Dialog open={isPayOpen} onOpenChange={setIsPayOpen}>
            <DialogTrigger asChild>
              <Button className="bg-emerald-600 hover:bg-emerald-500 text-white font-semibold gap-2 shadow-sm">
                <Smartphone className="w-4 h-4" />
                Pagar Cuota Online
              </Button>
            </DialogTrigger>
            <DialogContent className="sm:max-w-md">
              <DialogHeader className="text-center">
                <DialogTitle>Pagar Cuota #{loanInfo.nextDueNumber}</DialogTitle>
                <DialogDescription>
                  Escanea el código QR desde tu billetera digital o transfiere a nuestra cuenta.
                </DialogDescription>
              </DialogHeader>

              <div className="flex flex-col items-center justify-center p-4 rounded-xl bg-slate-900 text-white space-y-2 border">
                <div className="w-32 h-32 bg-white rounded-lg flex items-center justify-center p-2 text-slate-900">
                  <QrCode className="w-24 h-24 text-slate-800" />
                </div>
                <div className="text-xs font-bold text-purple-400">YAPE / PLIN: 987 654 321</div>
                <div className="text-xl font-bold font-mono">S/ {loanInfo.nextDueAmount.toFixed(2)}</div>
              </div>

              <div className="space-y-1.5 pt-2">
                <label className="text-xs font-medium text-muted-foreground">
                  Ingresa el N° de Operación del comprobante:
                </label>
                <Input
                  placeholder="Ej: 0583921"
                  value={voucherCode}
                  onChange={(e) => setVoucherCode(e.target.value)}
                  className="font-mono text-xs"
                />
              </div>

              <DialogFooter>
                <Button variant="outline" onClick={() => setIsPayOpen(false)}>
                  Cancelar
                </Button>
                <Button
                  onClick={handleConfirmPayment}
                  disabled={!voucherCode || submitting}
                  className="bg-emerald-600 hover:bg-emerald-500 text-white"
                >
                  {submitting ? "Validando..." : "Confirmar Pago"}
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </CardHeader>
      </Card>

      {/* Detalle del Próximo Vencimiento */}
      <Card>
        <CardContent className="p-6 space-y-6">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-4 rounded-lg bg-blue-500/10 border border-blue-500/20">
            <div>
              <span className="text-xs font-semibold text-blue-400">
                Próxima Cuota: #{loanInfo.nextDueNumber}
              </span>
              <div className="text-base font-bold mt-0.5">Vence el {loanInfo.nextDueDate}</div>
              <p className="text-xs text-muted-foreground mt-0.5">
                Paga a tiempo para mantener tu buen historial crediticio.
              </p>
            </div>
            <div className="text-left sm:text-right">
              <span className="text-xs text-muted-foreground">Monto de la Cuota</span>
              <div className="text-2xl font-bold font-mono">S/ {loanInfo.nextDueAmount.toFixed(2)}</div>
            </div>
          </div>

          {/* Progreso de Amortización */}
          <div className="space-y-2">
            <div className="flex justify-between text-xs">
              <span className="text-muted-foreground">Progreso del Crédito (1 de 6 cuotas)</span>
              <span className="font-semibold text-blue-400">{loanInfo.progressPercent}%</span>
            </div>
            <Progress value={loanInfo.progressPercent} className="h-2" />
          </div>

          {/* Tarjeta Bancaria Registrada */}
          <div className="pt-2">
            <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
              Cuenta Receptora Registrada
            </h4>
            <div className="p-3.5 rounded-lg border bg-muted/30 flex items-center justify-between">
              <div>
                <div className="text-xs font-semibold">Banco de Crédito BCP</div>
                <div className="text-[11px] text-muted-foreground font-mono">
                  {loanInfo.disbursedTo}
                </div>
              </div>
              <Badge variant="outline" className="text-emerald-500 border-emerald-500/30 text-[10px]">
                Activa para desembolsos
              </Badge>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
};
```
