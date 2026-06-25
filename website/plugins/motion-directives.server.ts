export default defineNuxtPlugin((nuxtApp) => {
  const noopDirective = {
    getSSRProps() {
      return {}
    },
  }

  nuxtApp.vueApp.directive('reveal', noopDirective)
  nuxtApp.vueApp.directive('parallax', noopDirective)
})
