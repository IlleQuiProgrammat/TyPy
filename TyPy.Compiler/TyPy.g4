grammar TyPy;

compilation_unit: statement+;
statement: INDENT* (import_stmt
    | from_import_stmt
    | variable_definition
    | function_definition
    | class_definition
    | expression
    | for_loop
    | while_loop
    | if_stmt
    | return_stmt
    | break
    | continue) NEWLINE;
    
operation: bitwise_operator | OP_POW | OP_MUL | OP_FLOOR_DIV | OP_DIV | OP_ADD | OP_SUB ;
bitwise_operator: BITWISE_NOT | BITWISE_AND | BITWISE_OR| BITWISE_XOR;

import_stmt: IMPORT VARIABLE;
from_import_stmt: FROM VARIABLE IMPORT comma_separated_variables;

variable_definition: VARIABLE (':' VARIABLE)? '=' expression;
function_definition: 'def' VARIABLE '(' typed_comma_separated_variables? ')' '->' VARIABLE ':';
class_definition: 'class' VARIABLE '(' comma_separated_variables ')' ':';
typed_comma_separated_variables: typed_variable (',' typed_variable)*;
typed_variable: VARIABLE ':' VARIABLE;
comma_separated_variables: VARIABLE (',' VARIABLE)*;
comma_separated_expressions: expression (',' expression)*;
expression: '(' expression ')'
    | expression operation expression
    | negate_expression
    | expression_var
    | get_item
    | list_comprehension | set_comprehension | dictionary_comprehension
    | function_call;
negate_expression: '-' expression;
expression_var: VARIABLE | NUMBER | STRING | BOOLEAN;
get_item: (expression_var | '(' expression ')') '[' expression? ':'? expression? (':' expression)? ']';
list_comprehension: '[' generator_expression ']';
set_comprehension: '{' generator_expression '}';
dictionary_comprehension: '{' expression ':' expression FOR comma_separated_variables IN expression (IF expression)? '}';
generator_expression: expression FOR comma_separated_variables IN expression (IF expression)?;
function_call: expression_var '(' comma_separated_expressions? ')' | '(' expression ')' '(' comma_separated_variables? ')';
for_loop: FOR comma_separated_variables IN expression ':';
while_loop: WHILE expression ':';
if_stmt: IF expression ':';
return_stmt: RETURN expression?;
break: BREAK;
continue: CONTINUE;
FOR: 'for';
IF: 'if';
WHILE: 'while';
RETURN: 'return';
BREAK: 'break';
CONTINUE: 'continue';
IN: 'in';
IS: 'is';
NOT: 'not';
IMPORT: 'import';
FROM: 'from';
BOOLEAN: 'False' | 'True';

AND: 'and';
OR: 'or';
OP_POW: '**';
OP_MUL: '*';
OP_FLOOR_DIV: '//';
OP_DIV: '/';
OP_ADD: '+';
OP_SUB: '-';

BITWISE_AND: '&';
BITWISE_OR: '|';
BITWISE_NOT: '~';
BITWISE_XOR: '^';

VARIABLE: (FIRST_VARIABLE_CHAR) SECOND_VARIABLE_CHAR*;
fragment FIRST_VARIABLE_CHAR: [a-zA-Z_.];
fragment SECOND_VARIABLE_CHAR: FIRST_VARIABLE_CHAR | [0-9];
NUMBER: '-'? [0-9]+;
FSTRING: 'f' STRING;
STRING: '""';
NEWLINE: '\n';
INDENT: (' ' | '\t');
WHITESPACE: (' ' | '\t') -> skip;