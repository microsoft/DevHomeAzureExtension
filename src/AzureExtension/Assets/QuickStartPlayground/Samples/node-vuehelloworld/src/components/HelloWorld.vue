<template>
    <div class="hello">
        <h1>{{ msg }}</h1>
        <p>
            Hello World from Vue
        </p>
    </div>
</template>

<script>
export default {
    name: 'HelloWorld',
    props: {
        msg: String
    }
}
</script>