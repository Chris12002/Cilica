package dev.cilica

import java.util.concurrent.CompletableFuture
import java.util.concurrent.TimeUnit
import dev.cilica.CilicaSessionRuntime.Value as Value

private fun <T> CompletableFuture<T>.await(): T = get(15, TimeUnit.SECONDS)
// All host entry is forwarded to the main thread; waits occur on the test executor.
class Scenario(private val host: CilicaSessionActivityHost,
               private val main: (() -> CompletableFuture<Any?>) -> Any?) {
    private fun <T> ui(block: () -> CompletableFuture<T>): T {
        @Suppress("UNCHECKED_CAST")
        return main { block().thenApply { it as Any? } } as T
    }
    private lateinit var session: CilicaSessionRuntime.Handle
    private lateinit var snapshot: CilicaSessionRuntime.Handle
    private lateinit var fault: CilicaSessionRuntime.Handle
    private lateinit var detail: CilicaSessionRuntime.ExceptionDetails
    private var expected = "A\u0000\ud800"
    private var steps = 0
    private var total = 9007199254740993L
    fun prepare() {
        val label = ui { host.importUTF16(expected.toCharArray()) }
        session = (ui { host.invoke(1, listOf(Value.Reference(label))) }.value as Value.Reference).handle!!
        host.runtime.release(label).await()
        snapshot = (ui { host.invoke(4, listOf(Value.Reference(session))) }.value as Value.Reference).handle!!
        fault = ui { host.invoke(2, listOf(Value.Reference(session), Value.Int32(-1)), true) }.exception!!
        detail = host.runtime.exceptionDetails(fault).await()
    }
    fun background() {
        check(String(host.runtime.copyUTF16(snapshot).await()) == expected)
        check(host.runtime.exceptionDetails(fault).await() == detail)
    }
    fun resumed(cycle: Int) {
        val fields = (ui { host.invoke(3, listOf(Value.Reference(session))) }.value as Value.Record).fields
        check(fields == listOf(Value.Int32(steps), Value.Integer64(total), Value.Int32(expected.length)))
        val next = (ui { host.invoke(2, listOf(Value.Reference(session), Value.Int32(cycle))) }.value as Value.Record).fields
        steps++; total += cycle; expected += ":$cycle"
        check(next == listOf(Value.Int32(steps), Value.Integer64(total), Value.Int32(expected.length)))
        host.runtime.release(snapshot).await()
        snapshot = (ui { host.invoke(4, listOf(Value.Reference(session))) }.value as Value.Reference).handle!!
        check(host.runtime.exceptionDetails(fault).await() == detail)
    }
    fun finished() { check(steps == 10) }
}
