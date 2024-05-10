<template>
    <div class="calculator">
        <h1>Vue Calculator</h1>
        <input type="text" v-model="input" />
        <button @click="calculate">Calculate</button>
        <p>Result: {{ result }}</p>
    </div>
</template>

<script>
export default {
    name: 'Calculator',
    data() {
        return {
            input: '',
            result: 0
        }
    },
    methods: {
        calculate() {
            this.result = eval(this.input);
        }
    }
}
</script>