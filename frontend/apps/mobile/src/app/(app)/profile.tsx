import React, { useState } from 'react';
import {
  StyleSheet,
  View,
  Text,
  ScrollView,
  RefreshControl,
  TextInput,
  Switch,
} from 'react-native';
import { useRouter } from 'expo-router';
import { useProfile, BankAccountItem } from '@/features/profile';
import { useSessionStore } from '@/lib/session';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '@/components/ui/status-badge';
import { formatMoney, maskTail } from '@prestamos/utils';

export default function ProfileScreen() {
  const router = useRouter();
  const {
    profile,
    bankAccounts,
    coolingPeriodNotice,
    isLoading,
    refresh,
    requestAccountChangeOtp,
    setPrimaryAccount,
    requestCreditBalanceRefund,
  } = useProfile();

  const logout = useSessionStore((state) => state.logout);
  const biometricsEnabled = useSessionStore((state) => state.biometricsEnabled);
  const toggleBiometrics = useSessionStore((state) => state.toggleBiometrics);

  // FE-CP-18: Cambio de cuenta principal con OTP nuevo
  const [pendingAccountId, setPendingAccountId] = useState<string | null>(null);
  const [otpCode, setOtpCode] = useState('');

  const handleLogout = async () => {
    await logout('manual');
    router.replace('/(auth)/login');
  };

  const handleStartAccountChange = async (accountId: string) => {
    setPendingAccountId(accountId);
    setOtpCode('');
    await requestAccountChangeOtp(accountId);
  };

  const handleConfirmAccountChange = async () => {
    if (!pendingAccountId || otpCode.length !== 6) return;
    await setPrimaryAccount(pendingAccountId, otpCode);
    setPendingAccountId(null);
    setOtpCode('');
  };

  const primaryVerifiedAccount = bankAccounts.find((a) => a.isPrimary && a.isVerified);
  const creditBalance = profile?.creditBalance ?? '0.00';

  return (
    <ScrollView
      contentContainerStyle={styles.container}
      refreshControl={<RefreshControl refreshing={isLoading} onRefresh={refresh} />}>
      {/* Datos Personales con enmascaramiento (FE-NF-09) */}
      <Card style={styles.card}>
        <View style={styles.headerRow}>
          <Text style={styles.sectionTitle}>Datos Personales</Text>
          {profile?.kycStatus ? <StatusBadge kind="kyc" status={profile.kycStatus} /> : null}
        </View>
        <View style={styles.infoRow}>
          <Text style={styles.label}>Nombre:</Text>
          <Text style={styles.value}>{profile?.fullName ?? 'Cliente'}</Text>
        </View>
        <View style={styles.infoRow}>
          <Text style={styles.label}>Documento:</Text>
          <Text style={styles.value}>
            {profile?.docNumber
              ? `${profile.docType ?? 'DNI'} ${maskTail(profile.docNumber)}`
              : '—'}
          </Text>
        </View>
        <View style={styles.infoRow}>
          <Text style={styles.label}>Correo:</Text>
          <Text style={styles.value}>{profile?.email ?? '—'}</Text>
        </View>
        <View style={styles.infoRow}>
          <Text style={styles.label}>Celular:</Text>
          <Text style={styles.value}>{profile?.phone ? maskTail(profile.phone) : '—'}</Text>
        </View>

        <Button
          title="Completar / Actualizar KYC y Consentimientos (FE-CP-03..05)"
          variant="outline"
          onPress={() => router.push('/(app)/onboarding')}
        />
      </Card>

      {/* FE-CP-19: Saldo a favor y devolución a cuenta verificada */}
      <Card style={styles.card}>
        <View style={styles.headerRow}>
          <Text style={styles.sectionTitle}>Saldo a Favor</Text>
          <StatusBadge label="FE-CP-19" tone="info" />
        </View>
        <Text style={styles.creditAmount}>{formatMoney(creditBalance)}</Text>
        <Text style={styles.sectionSubtitle}>
          Si tienes saldo a favor por pagos en exceso, solicita la devolución a tu cuenta bancaria verificada.
        </Text>
        <Button
          title="Solicitar Devolución de Saldo a Favor"
          variant="secondary"
          disabled={Number(creditBalance) <= 0 || !primaryVerifiedAccount}
          isLoading={isLoading}
          onPress={() => {
            if (primaryVerifiedAccount) {
              void requestCreditBalanceRefund(primaryVerifiedAccount.id);
            }
          }}
        />
      </Card>

      {/* FE-CP-18: Cuentas de Desembolso + OTP nuevo + espera 24-48h anti SIM-swap */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Cuentas de Desembolso (FE-CP-18)</Text>
        <View style={styles.simSwapWarning}>
          <Text style={styles.simSwapWarningText}>
            Seguridad anti SIM-swap: Cambiar tu cuenta principal exige un OTP nuevo y aplica una espera de 24–48 horas. Durante la espera, los desembolsos se envían a la cuenta anterior.
          </Text>
        </View>

        {coolingPeriodNotice ? (
          <View style={styles.noticeBox}>
            <Text style={styles.noticeText}>{coolingPeriodNotice}</Text>
          </View>
        ) : null}

        {pendingAccountId ? (
          <Card style={styles.otpCard}>
            <Text style={styles.otpTitle}>Confirmar Cambio de Cuenta Principal</Text>
            <Text style={styles.sectionSubtitle}>
              Ingresa el OTP de 6 dígitos enviado a tu celular registrado:
            </Text>
            <TextInput
              style={styles.otpInput}
              keyboardType="number-pad"
              textContentType="oneTimeCode"
              autoComplete="sms-otp"
              maxLength={6}
              placeholder="000000"
              value={otpCode}
              onChangeText={(t) => setOtpCode(t.replace(/\D/g, ''))}
            />
            <View style={styles.otpActions}>
              <Button
                title="Cancelar"
                variant="outline"
                onPress={() => setPendingAccountId(null)}
                style={{ flex: 1 }}
              />
              <Button
                title="Confirmar con OTP"
                onPress={handleConfirmAccountChange}
                disabled={otpCode.length !== 6}
                isLoading={isLoading}
                style={{ flex: 1 }}
              />
            </View>
          </Card>
        ) : null}

        {bankAccounts.map((account) => (
          <BankAccountItem
            key={account.id}
            account={account}
            onMakePrimary={() => handleStartAccountChange(account.id)}
          />
        ))}
      </View>

      {/* MOV-03: Biometría y Atención (FE-CP-21) */}
      <Card style={styles.card}>
        <Text style={styles.sectionTitle}>Seguridad y Atención</Text>

        <View style={styles.bioRow}>
          <View style={{ flex: 1 }}>
            <Text style={styles.bioTitle}>Desbloqueo Biométrico (MOV-03)</Text>
            <Text style={styles.sectionSubtitle}>
              Usa Face ID o huella digital para reingresar rápidamente.
            </Text>
          </View>
          <Switch
            value={biometricsEnabled}
            onValueChange={(val) => {
              void toggleBiometrics(val);
            }}
          />
        </View>

        <Button
          title="Libro de Reclamaciones Virtual (FE-CP-21)"
          variant="outline"
          onPress={() => router.push('/(app)/complaints')}
        />
        <Button title="Cerrar Sesión" variant="danger" onPress={handleLogout} />
      </Card>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    padding: 16,
    gap: 16,
    backgroundColor: '#F9FAFB',
  },
  card: {
    gap: 12,
  },
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  section: {
    gap: 10,
  },
  sectionTitle: {
    fontSize: 17,
    fontWeight: '700',
    color: '#111827',
  },
  sectionSubtitle: {
    fontSize: 12,
    color: '#6B7280',
    lineHeight: 17,
  },
  creditAmount: {
    fontSize: 24,
    fontWeight: '700',
    color: '#059669',
  },
  simSwapWarning: {
    backgroundColor: '#FFFBEB',
    borderColor: '#FDE68A',
    borderWidth: 1,
    borderRadius: 8,
    padding: 10,
  },
  simSwapWarningText: {
    fontSize: 12,
    color: '#92400E',
    lineHeight: 17,
  },
  noticeBox: {
    backgroundColor: '#EFF6FF',
    borderColor: '#BFDBFE',
    borderWidth: 1,
    borderRadius: 8,
    padding: 10,
  },
  noticeText: {
    fontSize: 12,
    color: '#1E40AF',
    lineHeight: 17,
  },
  otpCard: {
    gap: 10,
    borderColor: '#208AEF',
    backgroundColor: '#F0F9FF',
  },
  otpTitle: {
    fontSize: 15,
    fontWeight: '700',
    color: '#0369A1',
  },
  otpInput: {
    height: 46,
    borderWidth: 1.5,
    borderColor: '#0284C7',
    borderRadius: 8,
    backgroundColor: '#FFFFFF',
    fontSize: 20,
    fontWeight: '700',
    textAlign: 'center',
    letterSpacing: 6,
    color: '#111827',
  },
  otpActions: {
    flexDirection: 'row',
    gap: 10,
  },
  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 4,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#F3F4F6',
  },
  label: {
    fontSize: 14,
    color: '#6B7280',
  },
  value: {
    fontSize: 14,
    fontWeight: '600',
    color: '#111827',
  },
  bioRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 12,
    paddingVertical: 6,
  },
  bioTitle: {
    fontSize: 14,
    fontWeight: '600',
    color: '#111827',
  },
});
