import React, { useState } from 'react';
import { StyleSheet, View, Text, TextInput, ScrollView, Switch } from 'react-native';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';
import { useOnboarding, bankAccountSchema } from '@/features/onboarding';
import { maskTail } from '@prestamos/utils';
import type { KycDocumentPayload } from '@/features/onboarding';

export default function OnboardingScreen() {
  const {
    bankAccounts,
    cameraPermission,
    uploadedDocs,
    consentsSaved,
    isLoading,
    fieldErrors,
    requestCameraPermission,
    openSystemSettings,
    uploadKyc,
    submitConsents,
    addBankAccount,
    verifyBankAccount,
  } = useOnboarding();

  // FE-CP-04: Consentimientos separados por finalidad
  const [privacyAccepted, setPrivacyAccepted] = useState(false);
  const [creditBureauAccepted, setCreditBureauAccepted] = useState(false);
  const [marketingAccepted, setMarketingAccepted] = useState(false);

  // FE-CP-05: Cuenta de desembolso
  const [bankName, setBankName] = useState('BCP');
  const [accountType, setAccountType] = useState<
    'CUENTA_AHORRO' | 'CUENTA_CORRIENTE' | 'BILLETERA_DIGITAL'
  >('CUENTA_AHORRO');
  const [cci, setCci] = useState('');
  const [phoneWallet, setPhoneWallet] = useState('');
  const [localError, setLocalError] = useState<string | null>(null);

  const handleCapture = async (docType: KycDocumentPayload['docType']) => {
    if (cameraPermission !== 'granted') {
      const granted = await requestCameraPermission(false);
      if (!granted) return;
    }
    await uploadKyc({
      docType,
      fileUri: `file:///tmp/kyc-${docType.toLowerCase()}-compressed.jpg`,
    });
  };

  const handleSaveConsents = async () => {
    if (!privacyAccepted || !creditBureauAccepted) return;
    await submitConsents({
      privacyAccepted,
      creditBureauAccepted,
      marketingAccepted,
    });
  };

  const handleAddAccount = async () => {
    const parsed = bankAccountSchema.safeParse({
      bankName,
      accountType,
      cci: accountType !== 'BILLETERA_DIGITAL' ? cci : undefined,
      phoneWallet: accountType === 'BILLETERA_DIGITAL' ? phoneWallet : undefined,
    });

    if (!parsed.success) {
      setLocalError(parsed.error.issues[0]?.message ?? 'Datos de cuenta inválidos');
      return;
    }

    setLocalError(null);
    await addBankAccount(parsed.data);
    setCci('');
    setPhoneWallet('');
  };

  return (
    <ScrollView contentContainerStyle={styles.container}>
      {/* FE-CP-03 + MOV-05: Onboarding KYC con Cámara y Permisos */}
      <Card style={styles.card}>
        <View style={styles.headerRow}>
          <Text style={styles.sectionTitle}>1. Verificación de Identidad (KYC)</Text>
          <StatusBadge label="FE-CP-03 · MOV-05" tone="info" />
        </View>
        <Text style={styles.desc}>
          Captura tu DNI por ambos lados y una selfie con prueba de vida. Las imágenes se comprimen antes de subirse.
        </Text>

        {cameraPermission === 'denied' ? (
          <View style={styles.deniedBox}>
            <Text style={styles.deniedTitle}>Permiso de cámara denegado</Text>
            <Text style={styles.deniedDesc}>
              Necesitamos acceso a la cámara para verificar tu identidad con tu DNI y selfie. Habilítalo en los Ajustes de tu dispositivo.
            </Text>
            <Button
              title="Abrir Ajustes del Dispositivo"
              variant="outline"
              onPress={openSystemSettings}
            />
          </View>
        ) : (
          <View style={styles.kycButtons}>
            <View style={styles.kycRow}>
              <Text style={styles.kycLabel}>DNI Anverso</Text>
              {uploadedDocs.DNI_FRONT ? (
                <StatusBadge label="Subido" tone="success" />
              ) : (
                <Button
                  title="Capturar Anverso"
                  variant="outline"
                  onPress={() => handleCapture('DNI_FRONT')}
                  isLoading={isLoading}
                />
              )}
            </View>

            <View style={styles.kycRow}>
              <Text style={styles.kycLabel}>DNI Reverso</Text>
              {uploadedDocs.DNI_BACK ? (
                <StatusBadge label="Subido" tone="success" />
              ) : (
                <Button
                  title="Capturar Reverso"
                  variant="outline"
                  onPress={() => handleCapture('DNI_BACK')}
                  isLoading={isLoading}
                />
              )}
            </View>

            <View style={styles.kycRow}>
              <Text style={styles.kycLabel}>Selfie con prueba de vida</Text>
              {uploadedDocs.SELFIE ? (
                <StatusBadge label="Subido" tone="success" />
              ) : (
                <Button
                  title="Tomar Selfie"
                  variant="outline"
                  onPress={() => handleCapture('SELFIE')}
                  isLoading={isLoading}
                />
              )}
            </View>

            <Button
              title="Simular denegación de cámara (MOV-05)"
              variant="secondary"
              onPress={() => requestCameraPermission(true)}
            />
          </View>
        )}
      </Card>

      {/* FE-CP-04: Consentimientos separados por finalidad */}
      <Card style={styles.card}>
        <View style={styles.headerRow}>
          <Text style={styles.sectionTitle}>2. Consentimientos Legales</Text>
          <StatusBadge label="FE-CP-04" tone="info" />
        </View>
        <Text style={styles.desc}>
          Autorizaciones independientes por finalidad conforme a la Ley de Protección de Datos Personales.
        </Text>

        <View style={styles.consentRow}>
          <View style={styles.consentTextCol}>
            <Text style={styles.consentTitle}>Tratamiento de datos personales (Obligatorio)</Text>
            <Text style={styles.consentSub}>Para la ejecución de la relación contractual.</Text>
          </View>
          <Switch value={privacyAccepted} onValueChange={setPrivacyAccepted} />
        </View>

        <View style={styles.consentRow}>
          <View style={styles.consentTextCol}>
            <Text style={styles.consentTitle}>Consulta crediticia e historial (Obligatorio)</Text>
            <Text style={styles.consentSub}>Evaluación en centrales de riesgo (SBS / Infocorp).</Text>
          </View>
          <Switch value={creditBureauAccepted} onValueChange={setCreditBureauAccepted} />
        </View>

        <View style={styles.consentRow}>
          <View style={styles.consentTextCol}>
            <Text style={styles.consentTitle}>Comunicaciones comerciales (Opcional)</Text>
            <Text style={styles.consentSub}>Ofertas y pre-aprobaciones por SMS, WhatsApp o correo.</Text>
          </View>
          <Switch value={marketingAccepted} onValueChange={setMarketingAccepted} />
        </View>

        {consentsSaved ? (
          <StatusBadge label="Consentimientos registrados" tone="success" />
        ) : (
          <Button
            title="Guardar Consentimientos"
            onPress={handleSaveConsents}
            disabled={!privacyAccepted || !creditBureauAccepted}
            isLoading={isLoading}
          />
        )}
      </Card>

      {/* FE-CP-05: Cuenta de desembolso (CCI 20 dígitos o billetera) y verificación */}
      <Card style={styles.card}>
        <View style={styles.headerRow}>
          <Text style={styles.sectionTitle}>3. Cuenta de Desembolso</Text>
          <StatusBadge label="FE-CP-05" tone="info" />
        </View>
        <Text style={styles.desc}>
          La cuenta debe estar a tu nombre. Ingresa el CCI de 20 dígitos o tu billetera digital (Yape/Plin).
        </Text>

        <View style={styles.typeRow}>
          <Button
            title="Ahorros (CCI)"
            variant={accountType === 'CUENTA_AHORRO' ? 'primary' : 'outline'}
            onPress={() => setAccountType('CUENTA_AHORRO')}
            style={styles.typeBtn}
          />
          <Button
            title="Billetera"
            variant={accountType === 'BILLETERA_DIGITAL' ? 'primary' : 'outline'}
            onPress={() => setAccountType('BILLETERA_DIGITAL')}
            style={styles.typeBtn}
          />
        </View>

        <View style={styles.field}>
          <Text style={styles.label}>Entidad Financiera / Billetera</Text>
          <TextInput
            style={styles.input}
            value={bankName}
            onChangeText={setBankName}
            placeholder="BCP, BBVA, Interbank, Yape..."
          />
        </View>

        {accountType === 'BILLETERA_DIGITAL' ? (
          <View style={styles.field}>
            <Text style={styles.label}>Celular afiliado (9 dígitos)</Text>
            <TextInput
              style={styles.input}
              keyboardType="number-pad"
              maxLength={9}
              value={phoneWallet}
              onChangeText={setPhoneWallet}
              placeholder="987654321"
            />
          </View>
        ) : (
          <View style={styles.field}>
            <Text style={styles.label}>Código de Cuenta Interbancario (CCI - 20 dígitos)</Text>
            <TextInput
              style={styles.input}
              keyboardType="number-pad"
              maxLength={20}
              value={cci}
              onChangeText={setCci}
              placeholder="00219100123456789012"
            />
          </View>
        )}

        {localError || fieldErrors.cci ? (
          <Text style={styles.errorText}>{localError ?? fieldErrors.cci}</Text>
        ) : null}

        <Button
          title="Registrar Cuenta de Desembolso"
          onPress={handleAddAccount}
          isLoading={isLoading}
        />

        {bankAccounts.length > 0 ? (
          <View style={styles.accountsList}>
            <Text style={styles.subHeader}>Cuentas Registradas</Text>
            {bankAccounts.map((acc) => (
              <View key={acc.id} style={styles.accountItem}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.accBank}>{acc.bankName}</Text>
                  <Text style={styles.accMask}>
                    {acc.cci ? `CCI: ${maskTail(acc.cci)}` : maskTail(acc.accountNumberMasked)}
                  </Text>
                </View>
                {acc.isVerified ? (
                  <StatusBadge label="Verificada" tone="success" />
                ) : (
                  <Button
                    title="Verificar titularidad"
                    variant="outline"
                    onPress={() => verifyBankAccount(acc.id)}
                    isLoading={isLoading}
                  />
                )}
              </View>
            ))}
          </View>
        ) : null}
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
    gap: 14,
  },
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  sectionTitle: {
    fontSize: 17,
    fontWeight: '700',
    color: '#111827',
    flex: 1,
  },
  desc: {
    fontSize: 13,
    color: '#6B7280',
    lineHeight: 18,
  },
  deniedBox: {
    backgroundColor: '#FEF2F2',
    borderColor: '#FECACA',
    borderWidth: 1,
    borderRadius: 10,
    padding: 14,
    gap: 10,
  },
  deniedTitle: {
    fontSize: 15,
    fontWeight: '700',
    color: '#991B1B',
  },
  deniedDesc: {
    fontSize: 13,
    color: '#7F1D1D',
    lineHeight: 18,
  },
  kycButtons: {
    gap: 12,
  },
  kycRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 6,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#E5E7EB',
  },
  kycLabel: {
    fontSize: 14,
    fontWeight: '600',
    color: '#374151',
  },
  consentRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 8,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#E5E7EB',
    gap: 12,
  },
  consentTextCol: {
    flex: 1,
    gap: 2,
  },
  consentTitle: {
    fontSize: 14,
    fontWeight: '600',
    color: '#111827',
  },
  consentSub: {
    fontSize: 12,
    color: '#6B7280',
  },
  typeRow: {
    flexDirection: 'row',
    gap: 10,
  },
  typeBtn: {
    flex: 1,
  },
  field: {
    gap: 4,
  },
  label: {
    fontSize: 13,
    fontWeight: '500',
    color: '#374151',
  },
  input: {
    height: 46,
    borderWidth: 1,
    borderColor: '#D1D5DB',
    borderRadius: 8,
    paddingHorizontal: 12,
    fontSize: 15,
    backgroundColor: '#F9FAFB',
    color: '#111827',
  },
  errorText: {
    fontSize: 12,
    color: '#DC2626',
  },
  accountsList: {
    marginTop: 8,
    gap: 10,
  },
  subHeader: {
    fontSize: 14,
    fontWeight: '700',
    color: '#374151',
  },
  accountItem: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: 10,
    backgroundColor: '#F9FAFB',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#E5E7EB',
  },
  accBank: {
    fontSize: 14,
    fontWeight: '600',
    color: '#111827',
  },
  accMask: {
    fontSize: 12,
    color: '#6B7280',
  },
});
