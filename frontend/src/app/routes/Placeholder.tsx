import { useTranslation } from 'react-i18next'

export function Placeholder({ labelKey }: { labelKey: string }) {
  const { t } = useTranslation()
  return (
    <section className="col-span-full py-16 text-center">
      <h1 className="text-lg font-semibold text-primary">
        {t('placeholder.coming_soon', { section: t(labelKey) })}
      </h1>
      <p className="mt-2 text-sm text-muted-fg">{t('placeholder.coming_soon_detail')}</p>
    </section>
  )
}
