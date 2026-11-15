<script setup lang="ts">
const { localeProperties } = useI18n()

// lang + dir follow the active locale; logical CSS properties do the rest of the RTL work.
useHead({
  htmlAttrs: {
    lang: () => localeProperties.value.language ?? 'ar',
    dir: () => localeProperties.value.dir ?? 'rtl',
  },
})
</script>

<template>
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
  <UiToaster />
  <UiConfirmHost />
</template>
